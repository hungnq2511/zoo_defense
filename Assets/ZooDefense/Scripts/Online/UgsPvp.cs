using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using ZooDefense.Core;
using ZooDefense.Game;

namespace ZooDefense.Online
{
    /// <summary>
    /// Online 1v1 over Unity Gaming Services:
    ///   1. anonymous sign-in (one player id per device, kept between launches),
    ///   2. ranked pairing by hidden rating: quick-join a 2-player session whose host rating is within a window
    ///      (±150, then ±300, ±600, anyone) that widens each time we re-roll, or create one and wait,
    ///      friendly rooms: a private session joined with its 6-character code,
    ///   3. relay connection between the two devices (UtpLink), then the Hello/Start handshake.
    /// A ranked search that takes too long sets State = Failed: the matchmaking screen then offers a bot.
    /// During the match a dropped relay link is rebuilt by UtpLink (Reconnecting is true meanwhile).
    /// </summary>
    public sealed class UgsPvp : IOnlinePvp
    {
        const string SessionType = "zoo_pvp", RoomType = "zoo_room";
        /// <summary>Waiting alone in our own session this long: leave it and quick-join again with a wider rating window.</summary>
        const float RerollAfter = 3f;
        /// <summary>A friendly room waits this long for the friend.</summary>
        const float RoomWait = 600f;
        /// <summary>Connected but the handshake does not finish.</summary>
        const float HandshakeTimeout = 10f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            ZooDefense.Game.Online.Pvp = new UgsPvp();
        }

        OnlineState _state = OnlineState.Idle;
        string _error;
        ISession _session;
        UtpLink _link;
        OnlineHandshake _handshake;
        OnlineHello _me;
        int _attempt;
        float _searchTime, _aloneTime, _handshakeTime, _giveUpAfter = 10f;
        bool _room;
        string _joinCode;
        bool _busy;          // an async call is in flight
        int _generation;     // bumped by Leave(): late async results of an old search are ignored

        public OnlineState State { get { return _state; } }
        public string Error { get { return _error; } }
        public string PlayerId { get { return UgsCore.SignedIn ? AuthenticationService.Instance.PlayerId : null; } }
        public OnlineMatchInfo Match { get; private set; }
        public string RoomCode { get; private set; }
        public bool Reconnecting { get { return _state == OnlineState.Ready && _link != null && !_link.Connected; } }

        public void StartSearch(OnlineHello me, float giveUpAfter)
        {
            Begin(me, false, null, Mathf.Max(2f, giveUpAfter));
        }

        public void CreateRoom(OnlineHello me) { Begin(me, true, null, RoomWait); }

        public void JoinRoom(OnlineHello me, string code) { Begin(me, true, code, 25f); }

        void Begin(OnlineHello me, bool room, string joinCode, float giveUpAfter)
        {
            Leave();
            _me = me;
            _room = room;
            _joinCode = joinCode;
            _giveUpAfter = giveUpAfter;
            _attempt = 0;
            _error = null;
            _searchTime = _aloneTime = _handshakeTime = 0f;
            Match = null;
            RoomCode = null;
            _state = OnlineState.SigningIn;
            int gen = _generation;
            Run(gen, SignInAsync, () =>
            {
                if (_state != OnlineState.SigningIn) return;
                if (!_room) BeginPairing(gen);
                else if (string.IsNullOrEmpty(_joinCode)) BeginRoom(gen);
                else BeginJoin(gen);
            });
        }

        /// <summary>Friendly room host: a private session; its code is shown to the friend.</summary>
        void BeginRoom(int gen)
        {
            _state = OnlineState.Searching;
            _link = new UtpLink();
            var link = _link;
            Run(gen, async () =>
            {
                var options = new SessionOptions
                {
                    Type = RoomType,
                    MaxPlayers = 2,
                    IsPrivate = true,
                    SessionProperties = new Dictionary<string, SessionProperty>
                    {
                        { "ver", new SessionProperty(VersionKey, VisibilityPropertyOptions.Public, PropertyIndex.String1) },
                    },
                }.WithRelayNetwork().WithNetworkHandler(link);
                var session = await MultiplayerService.Instance.CreateSessionAsync(options);
                if (gen != _generation) { try { await session.LeaveAsync(); } catch { } return; }
                _session = session;
                RoomCode = session.Code;
                Debug.Log("[Online] room " + session.Code);
            }, null);
        }

        /// <summary>Friendly room guest: join by code.</summary>
        void BeginJoin(int gen)
        {
            _state = OnlineState.Searching;
            _link = new UtpLink();
            var link = _link;
            string code = _joinCode.Trim().ToUpperInvariant();
            Run(gen, async () =>
            {
                ISession session;
                try { session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code, new JoinSessionOptions().WithNetworkHandler(link)); }
                catch (SessionException e)
                {
                    Debug.LogWarning("[Online] join room: " + e.Error + " " + e.Message);
                    if (gen == _generation) { Fail("room_not_found"); }
                    return;
                }
                if (gen != _generation) { try { await session.LeaveAsync(); } catch { } return; }
                string ver;
                SessionProperty prop;
                ver = session.Properties != null && session.Properties.TryGetValue("ver", out prop) ? prop.Value : null;
                if (ver != null && ver != VersionKey) { _session = session; Fail("version"); return; }
                _session = session;
                Debug.Log("[Online] joined room " + code);
            }, null);
        }

        Task SignInAsync() { return UgsCore.EnsureSignedInAsync(); }

        void BeginPairing(int gen)
        {
            _state = OnlineState.Searching;
            _aloneTime = 0f;
            _link = new UtpLink();
            var link = _link;
            Run(gen, async () =>
            {
                int window = MatchmakingRules.Window(_attempt);
                var filters = new List<FilterOption>
                {
                    new FilterOption(FilterField.AvailableSlots, "1", FilterOperation.GreaterOrEqual),
                    new FilterOption(FilterField.StringIndex1, VersionKey, FilterOperation.Equal),
                };
                if (window < 100000)
                {
                    filters.Add(new FilterOption(FilterField.NumberIndex1, Mathf.Max(0, _me.mmr - window).ToString(), FilterOperation.GreaterOrEqual));
                    filters.Add(new FilterOption(FilterField.NumberIndex1, (_me.mmr + window).ToString(), FilterOperation.LessOrEqual));
                }
                var quick = new QuickJoinOptions
                {
                    Filters = filters,
                    Timeout = TimeSpan.FromSeconds(3),
                    CreateSession = true,
                };
                var options = new SessionOptions
                {
                    Type = SessionType,
                    MaxPlayers = 2,
                    IsPrivate = false,
                    SessionProperties = new Dictionary<string, SessionProperty>
                    {
                        { "ver", new SessionProperty(VersionKey, VisibilityPropertyOptions.Public, PropertyIndex.String1) },
                        { "mmr", new SessionProperty(_me.mmr.ToString(), VisibilityPropertyOptions.Public, PropertyIndex.Number1) },
                    },
                }.WithRelayNetwork().WithNetworkHandler(link);
                var session = await MultiplayerService.Instance.MatchmakeSessionAsync(quick, options);
                if (gen != _generation) { try { await session.LeaveAsync(); } catch { } return; }
                _session = session;
                Debug.Log("[Online] session " + session.Id + (session.IsHost ? " (host, waiting)" : " (joined)") + " players " + session.PlayerCount + " window ±" + window);
            }, null);
        }

        /// <summary>Protocol + content: only players running the same game data are paired (rating does the rest).</summary>
        string VersionKey { get { return "p" + LockstepSync.ProtocolVersion + "_c" + (_me.content & 0xFFFF); } }

        public void Update(float dt)
        {
            if (_link != null) _link.Update();
            switch (_state)
            {
                case OnlineState.SigningIn:
                case OnlineState.Searching:
                    _searchTime += dt;
                    if (_searchTime > _giveUpAfter) { Fail("timeout"); return; }
                    if (_state == OnlineState.Searching && _session != null && !_busy)
                    {
                        if (_link.Connected)
                        {
                            _state = OnlineState.Handshake;
                            _handshakeTime = 0f;
                            _handshake = new OnlineHandshake(_link, _session.IsHost, _me, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                        }
                        else if (!_room && _session.IsHost && _session.PlayerCount < 2)
                        {
                            _aloneTime += dt;
                            if (_aloneTime > RerollAfter && _searchTime < _giveUpAfter - 1.5f) Reroll();
                        }
                    }
                    break;
                case OnlineState.Handshake:
                    _handshakeTime += dt;
                    if (_link.Disconnected) { Fail("disconnected"); return; }
                    if (_handshake.Update())
                    {
                        if (_handshake.Error != null) { Fail(_handshake.Error); return; }
                        Match = _handshake.Result;
                        _state = OnlineState.Ready;
                        LockSession();
                    }
                    else if (_handshakeTime > HandshakeTimeout) Fail("handshake");
                    break;
            }
        }

        /// <summary>Nobody joined our session: leave it and look again (two lone hosts would otherwise wait forever).</summary>
        void Reroll()
        {
            int gen = _generation;
            var old = _session;
            var oldLink = _link;
            _session = null;
            _link = null;
            Run(gen, async () =>
            {
                try { await old.LeaveAsync(); } catch (Exception e) { Debug.LogWarning("[Online] leave: " + e.Message); }
                oldLink.Dispose();
            }, () => { if (_state == OnlineState.Searching) { _attempt++; BeginPairing(gen); } });
        }

        /// <summary>Full match: hide the session from quick-join.</summary>
        async void LockSession()
        {
            try
            {
                if (_session == null || !_session.IsHost) return;
                var host = _session.AsHost();
                host.IsLocked = true;
                await host.SavePropertiesAsync();
            }
            catch (Exception e) { Debug.LogWarning("[Online] lock: " + e.Message); }
        }

        void Fail(string why)
        {
            Debug.LogWarning("[Online] search failed: " + why);
            string keep = why;
            Leave();
            _error = keep;
            _state = OnlineState.Failed;
        }

        public void Leave()
        {
            _generation++;
            var session = _session;
            var link = _link;
            _session = null;
            _link = null;
            _handshake = null;
            Match = null;
            RoomCode = null;
            _busy = false;
            _state = OnlineState.Idle;
            if (link != null && link.Connected)
            {
                link.Send(new[] { (byte)NetMsg.Bye });
                link.Update();
            }
            if (session != null) LeaveLater(session, link);
            else if (link != null) link.Dispose();
        }

        static async void LeaveLater(ISession session, UtpLink link)
        {
            try { await session.LeaveAsync(); }
            catch (Exception e) { Debug.LogWarning("[Online] leave: " + e.Message); }
            if (link != null) link.Dispose();
        }

        /// <summary>Runs an async step; errors end the search (bot fallback). Results of an abandoned search are dropped.</summary>
        async void Run(int gen, Func<Task> work, Action then)
        {
            _busy = true;
            try
            {
                await work();
                if (gen != _generation) return;
                _busy = false;
                if (then != null) then();
            }
            catch (Exception e)
            {
                if (gen != _generation) return;
                _busy = false;
                Debug.LogWarning("[Online] " + e.GetType().Name + ": " + e.Message);
                Fail(e is AuthenticationException || e is RequestFailedException ? "service" : "error");
            }
        }
    }
}
