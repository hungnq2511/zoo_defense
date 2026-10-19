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
    ///   2. pairing: quick-join a 2-player session of the same arena, or create one and wait,
    ///   3. relay connection between the two devices (UtpLink), then the Hello/Start handshake.
    /// Any failure or a long wait sets State = Failed: the matchmaking screen then falls back to a bot.
    /// </summary>
    public sealed class UgsPvp : IOnlinePvp
    {
        const string SessionType = "zoo_pvp";
        /// <summary>Waiting alone in our own session this long: leave it and quick-join again (someone else may be waiting).</summary>
        const float RerollAfter = 5f;
        /// <summary>Total search time before giving up (bot fallback).</summary>
        const float GiveUpAfter = 10f;
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
        int _bracket;
        float _searchTime, _aloneTime, _handshakeTime;
        bool _busy;          // an async call is in flight
        int _generation;     // bumped by Leave(): late async results of an old search are ignored

        public OnlineState State { get { return _state; } }
        public string Error { get { return _error; } }
        public string PlayerId { get { return UgsCore.SignedIn ? AuthenticationService.Instance.PlayerId : null; } }
        public OnlineMatchInfo Match { get; private set; }

        public void StartSearch(OnlineHello me, int bracket)
        {
            Leave();
            _me = me;
            _bracket = bracket;
            _error = null;
            _searchTime = _aloneTime = _handshakeTime = 0f;
            Match = null;
            _state = OnlineState.SigningIn;
            int gen = _generation;
            Run(gen, SignInAsync, () => { if (_state == OnlineState.SigningIn) BeginPairing(gen); });
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
                var quick = new QuickJoinOptions
                {
                    Filters = new List<FilterOption>
                    {
                        new FilterOption(FilterField.AvailableSlots, "1", FilterOperation.GreaterOrEqual),
                        new FilterOption(FilterField.StringIndex1, BracketKey, FilterOperation.Equal),
                    },
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
                        { "bracket", new SessionProperty(BracketKey, VisibilityPropertyOptions.Public, PropertyIndex.String1) },
                    },
                }.WithRelayNetwork().WithNetworkHandler(link);
                var session = await MultiplayerService.Instance.MatchmakeSessionAsync(quick, options);
                if (gen != _generation) { try { await session.LeaveAsync(); } catch { } return; }
                _session = session;
                Debug.Log("[Online] session " + session.Id + (session.IsHost ? " (host, waiting)" : " (joined)") + " players " + session.PlayerCount);
            }, null);
        }

        /// <summary>Arena bracket + protocol + content: only compatible players are paired.</summary>
        string BracketKey { get { return "a" + _bracket + "_p" + LockstepSync.ProtocolVersion + "_c" + (_me.content & 0xFFFF); } }

        public void Update(float dt)
        {
            if (_link != null) _link.Update();
            switch (_state)
            {
                case OnlineState.SigningIn:
                case OnlineState.Searching:
                    _searchTime += dt;
                    if (_searchTime > GiveUpAfter) { Fail("timeout"); return; }
                    if (_state == OnlineState.Searching && _session != null && !_busy)
                    {
                        if (_link.Connected)
                        {
                            _state = OnlineState.Handshake;
                            _handshakeTime = 0f;
                            _handshake = new OnlineHandshake(_link, _session.IsHost, _me, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                        }
                        else if (_session.IsHost && _session.PlayerCount < 2)
                        {
                            _aloneTime += dt;
                            if (_aloneTime > RerollAfter && _searchTime < GiveUpAfter - 4f) Reroll();
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
            }, () => { if (_state == OnlineState.Searching) BeginPairing(gen); });
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
