using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>What each player tells the other before the match: who they are and what they bring.</summary>
    [Serializable]
    public class OnlineHello
    {
        public int protocol = LockstepSync.ProtocolVersion;
        public int content;            // content fingerprint: both sides must run the same game data
        public string name;
        public string title;
        public int trophies;
        public string[] deck;
        public string[] sendDeck;
        public int[] levels;
        public string[] spells;
    }

    /// <summary>Sent by the host once both hellos are known: the full match setup (seed, both players, rules).</summary>
    [Serializable]
    public class OnlineStart
    {
        public MatchSetup setup;
        public int hostTrophies, guestTrophies;
        public string hostTitle, guestTitle;
    }

    /// <summary>A found online opponent, ready to play.</summary>
    public sealed class OnlineMatchInfo
    {
        public MatchSetup setup;
        public int localPlayer;          // 0 = host, 1 = guest
        public string opponentName, opponentTitle;
        public int opponentTrophies;
        public string[] opponentDeck;
        public IPacketLink link;
    }

    public enum OnlineState { Idle, SigningIn, Searching, Handshake, Ready, Failed }

    /// <summary>
    /// Online 1v1 provider. Implemented by the ZooDefense.Online assembly (Unity Gaming Services: sign-in, pairing,
    /// relay connection). When that assembly is missing, Online.Pvp is null and the game plays against bots.
    /// </summary>
    public interface IOnlinePvp
    {
        OnlineState State { get; }
        string Error { get; }
        string PlayerId { get; }
        /// <summary>Starts sign-in + pairing with players of the same arena (bracket).</summary>
        void StartSearch(OnlineHello me, int bracket);
        /// <summary>Must be called every frame while searching or playing (pumps the network).</summary>
        void Update(float dt);
        OnlineMatchInfo Match { get; }
        /// <summary>Stops searching / leaves the current game.</summary>
        void Leave();
    }

    public static class Online
    {
        /// <summary>Set by the ZooDefense.Online assembly at startup; null = offline build.</summary>
        public static IOnlinePvp Pvp;

        /// <summary>Content fingerprint: different game data on the two sides would desync at once.</summary>
        public static int ContentFingerprint(ContentDatabase c)
        {
            unchecked
            {
                int h = 17;
                foreach (var u in c.Units) { h = h * 31 + u.id.GetHashCode(); h = h * 31 + u.damage; h = h * 31 + u.attackIntervalTicks; }
                foreach (var e in c.Enemies) { h = h * 31 + e.id.GetHashCode(); h = h * 31 + e.hp; }
                var b = c.Balance;
                h = h * 31 + b.hpGrowthPercentPerWave; h = h * 31 + b.waveHpStartPermille; h = h * 31 + b.elementBonusPercent;
                return h;
            }
        }

        public static OnlineHello MyHello(GameApp app)
        {
            var p = app.Profile;
            return new OnlineHello
            {
                content = ContentFingerprint(app.Content),
                name = p.playerName, title = app.Meta.State.title, trophies = app.Meta.State.trophies,
                deck = (string[])p.deck.Clone(), sendDeck = (string[])p.sendDeck.Clone(),
                levels = app.Meta.DeckLevels(p.deck), spells = (string[])p.spells.Clone(),
            };
        }

        public static PlayerSetup ToSetup(OnlineHello h)
        {
            return new PlayerSetup { name = h.name, deck = h.deck, sendDeck = h.sendDeck, cardLevels = h.levels, spells = h.spells };
        }

        // ------------------------------------------------------------------ message helpers (JSON inside a typed packet)

        public static byte[] Pack(NetMsg kind, object payload)
        {
            var json = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            var data = new byte[json.Length + 1];
            data[0] = (byte)kind;
            Buffer.BlockCopy(json, 0, data, 1, json.Length);
            return data;
        }

        public static T Unpack<T>(byte[] data)
        {
            return JsonUtility.FromJson<T>(Encoding.UTF8.GetString(data, 1, data.Length - 1));
        }
    }

    /// <summary>
    /// Hello / Start exchange once the two devices are connected. The host (player 0) picks the seed and the rules
    /// (including this week's live event) and sends the whole setup, so both sides build the exact same Match.
    /// </summary>
    public sealed class OnlineHandshake
    {
        readonly IPacketLink _link;
        readonly bool _host;
        readonly OnlineHello _me;
        readonly long _now;
        bool _sentHello;
        public OnlineMatchInfo Result { get; private set; }
        public string Error { get; private set; }

        public OnlineHandshake(IPacketLink link, bool host, OnlineHello me, long nowUnix)
        {
            _link = link; _host = host; _me = me; _now = nowUnix;
        }

        /// <summary>Call every frame after the link is connected. Returns true once Result (or Error) is set.</summary>
        public bool Update()
        {
            if (Result != null || Error != null) return true;
            if (!_link.Connected) return false;
            if (!_host && !_sentHello) { _link.Send(Online.Pack(NetMsg.Hello, _me)); _sentHello = true; }
            byte[] data;
            while (_link.TryReceive(out data))
            {
                if (data == null || data.Length < 1) continue;
                if (_host && data[0] == (byte)NetMsg.Hello)
                {
                    var guest = Online.Unpack<OnlineHello>(data);
                    if (guest.protocol != _me.protocol || guest.content != _me.content) { Error = "version"; return true; }
                    var rules = new MatchRules { mode = MatchMode.Versus };
                    LiveOps.ApplyTo(LiveOps.Of(GameCalendar.WeekIndex(_now)), rules);
                    var start = new OnlineStart
                    {
                        setup = new MatchSetup { seed = UnityEngine.Random.Range(1, int.MaxValue), players = new[] { Online.ToSetup(_me), Online.ToSetup(guest) }, rules = rules },
                        hostTrophies = _me.trophies, guestTrophies = guest.trophies, hostTitle = _me.title, guestTitle = guest.title,
                    };
                    var packed = Online.Pack(NetMsg.Start, start);
                    _link.Send(packed);
                    // Use the setup exactly as the guest will read it (JSON turns null arrays into empty ones, etc.).
                    start = Online.Unpack<OnlineStart>(packed);
                    Result = new OnlineMatchInfo { setup = start.setup, localPlayer = 0, opponentName = guest.name, opponentTitle = guest.title, opponentTrophies = guest.trophies, opponentDeck = guest.deck, link = _link };
                    return true;
                }
                if (!_host && data[0] == (byte)NetMsg.Start)
                {
                    var start = Online.Unpack<OnlineStart>(data);
                    var host = start.setup.players[0];
                    Result = new OnlineMatchInfo { setup = start.setup, localPlayer = 1, opponentName = host.name, opponentTitle = start.hostTitle, opponentTrophies = start.hostTrophies, opponentDeck = host.deck, link = _link };
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>Lockstep match transport over the online link (see LockstepSync).</summary>
    public sealed class OnlineLockstepTransport : IMatchTransport
    {
        readonly IPacketLink _link;
        LockstepSync _sync;
        float _clock;

        public OnlineLockstepTransport(IPacketLink link) { _link = link; }

        public LockstepSync Sync { get { return _sync; } }

        public void Attach(Match match, int localPlayer) { _sync = new LockstepSync(match, localPlayer, _link); }
        public void SendLocal(Command command) { _sync.QueueLocal(command); }

        public bool CanStep()
        {
            _clock = Time.realtimeSinceStartup;
            _sync.Pump(_clock);
            return _sync.CanStep;
        }

        public void BeforeStep() { _sync.BeforeStep(); }
    }
}
