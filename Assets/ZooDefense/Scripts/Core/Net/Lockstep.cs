using System;
using System.Collections.Generic;

namespace ZooDefense.Core
{
    /// <summary>A reliable, ordered message pipe to the other player (Unity Transport over the relay, or a test loopback).</summary>
    public interface IPacketLink
    {
        bool Connected { get; }
        void Send(byte[] data);
        bool TryReceive(out byte[] data);
    }

    /// <summary>Online message kinds. Every packet starts with one of these bytes.</summary>
    public enum NetMsg : byte { Hello = 1, Start = 2, Frame = 3, Hash = 4, Bye = 5 }

    /// <summary>
    /// Deterministic lockstep for one match: only player commands travel over the network, never game state.
    /// Each tick T, each side sends its commands for tick T + InputDelay (an empty frame is still sent), and a
    /// tick can only be simulated once the other side's frame for it has arrived. Both sides therefore run the
    /// exact same command list, tick by tick, and stay identical. A state hash is exchanged every second to
    /// detect a desync early.
    /// </summary>
    public sealed class LockstepSync
    {
        public const int ProtocolVersion = 1;
        /// <summary>Input delay in ticks (4 = 0.2 s at 20 Hz): hides network latency below that.</summary>
        public readonly int InputDelay;
        public const int HashEveryTicks = 20;

        readonly Match _m;
        readonly int _local;
        readonly IPacketLink _link;
        readonly List<Command> _pendingLocal = new List<Command>();
        readonly Dictionary<int, List<Command>> _localFrames = new Dictionary<int, List<Command>>();
        readonly Dictionary<int, List<Command>> _remoteFrames = new Dictionary<int, List<Command>>();
        readonly Dictionary<int, uint> _myHashes = new Dictionary<int, uint>();
        readonly Dictionary<int, uint> _theirHashes = new Dictionary<int, uint>();
        int _sentUpTo = -1;
        double _lastReceive;

        public bool Desynced { get; private set; }
        public int DesyncTick { get; private set; }
        public bool RemoteLeft { get; private set; }
        /// <summary>The opponent stopped answering or left: the match continues locally and they forfeit.</summary>
        public bool Abandoned { get; private set; }
        /// <summary>Seconds since the last packet from the opponent (for the "waiting for opponent" overlay).</summary>
        public double SilentFor { get; private set; }
        public int RemoteFrameTick { get; private set; }

        public LockstepSync(Match match, int localPlayer, IPacketLink link, int inputDelay = 4)
        {
            _m = match; _local = localPlayer; _link = link;
            InputDelay = Math.Max(1, inputDelay);
            RemoteFrameTick = InputDelay - 1;
        }

        /// <summary>A command from this device; it will run on both devices at tick (now + InputDelay).</summary>
        public void QueueLocal(Command c)
        {
            c.player = _local;
            _pendingLocal.Add(c);
        }

        /// <summary>Reads everything that arrived. `now` = seconds (any clock); `timeout` = seconds of silence before a forfeit.</summary>
        public void Pump(double now, double timeout = 15.0)
        {
            if (_lastReceive == 0) _lastReceive = now;
            byte[] data;
            while (_link.TryReceive(out data))
            {
                _lastReceive = now;
                if (data == null || data.Length == 0) continue;
                switch ((NetMsg)data[0])
                {
                    case NetMsg.Frame:
                    {
                        int pos = 1;
                        int tick = ReadInt(data, ref pos);
                        int n = data[pos++];
                        var list = new List<Command>(n);
                        for (int i = 0; i < n; i++)
                        {
                            var type = (CommandType)data[pos++];
                            int a = ReadInt(data, ref pos), b = ReadInt(data, ref pos);
                            list.Add(new Command(tick, 1 - _local, type, a, b));
                        }
                        _remoteFrames[tick] = list;
                        if (tick > RemoteFrameTick) RemoteFrameTick = tick;
                        break;
                    }
                    case NetMsg.Hash:
                    {
                        int pos = 1;
                        int tick = ReadInt(data, ref pos);
                        uint hash = (uint)ReadInt(data, ref pos);
                        _theirHashes[tick] = hash;
                        CheckHash(tick);
                        break;
                    }
                    case NetMsg.Bye:
                        RemoteLeft = true;
                        break;
                }
            }
            SilentFor = now - _lastReceive;
            // A player who left (or whose connection closed) has already sent every frame up to their last tick, so
            // keep playing those first: the match may end normally on its own. Forfeit only when frames run out.
            bool gone = (RemoteLeft || !_link.Connected) && !HasRemoteFrame;
            if (!Abandoned && !_m.IsOver && (gone || SilentFor > timeout)) Abandon();
        }

        /// <summary>True when the opponent's frame for the next tick is here.</summary>
        public bool CanStep { get { return Abandoned || HasRemoteFrame; } }

        bool HasRemoteFrame { get { return _m.Tick < InputDelay || _remoteFrames.ContainsKey(_m.Tick); } }

        /// <summary>Call right before Match.Step(): sends our frame for Tick + InputDelay and feeds both frames for Tick.</summary>
        public void BeforeStep()
        {
            int tick = _m.Tick;
            int target = tick + InputDelay;
            if (target > _sentUpTo && !Abandoned)
            {
                var frame = new List<Command>(_pendingLocal.Count);
                foreach (var c in _pendingLocal) frame.Add(new Command(target, _local, c.type, c.a, c.b));
                _pendingLocal.Clear();
                _localFrames[target] = frame;
                SendFrame(target, frame);
                _sentUpTo = target;
            }
            else if (Abandoned && _pendingLocal.Count > 0)
            {
                // Playing on alone: our commands run right away.
                var frame = new List<Command>();
                foreach (var c in _pendingLocal) frame.Add(new Command(tick, _local, c.type, c.a, c.b));
                _pendingLocal.Clear();
                List<Command> existing;
                if (_localFrames.TryGetValue(tick, out existing)) existing.AddRange(frame); else _localFrames[tick] = frame;
            }

            List<Command> mine, theirs;
            // Same order on both devices: Match.Submit sorts by (tick, player).
            if (_localFrames.TryGetValue(tick, out mine)) { foreach (var c in mine) _m.Submit(c); _localFrames.Remove(tick); }
            if (_remoteFrames.TryGetValue(tick, out theirs)) { foreach (var c in theirs) _m.Submit(c); _remoteFrames.Remove(tick); }

            if (tick > 0 && tick % HashEveryTicks == 0 && !Abandoned)
            {
                uint h = _m.StateHash();
                _myHashes[tick] = h;
                var p = new byte[9];
                p[0] = (byte)NetMsg.Hash;
                int pos = 1;
                WriteInt(p, ref pos, tick);
                WriteInt(p, ref pos, (int)h);
                _link.Send(p);
                CheckHash(tick);
            }
        }

        void CheckHash(int tick)
        {
            uint a, b;
            if (_myHashes.TryGetValue(tick, out a) && _theirHashes.TryGetValue(tick, out b))
            {
                if (a != b && !Desynced) { Desynced = true; DesyncTick = tick; }
                _myHashes.Remove(tick);
                _theirHashes.Remove(tick);
            }
        }

        void Abandon()
        {
            Abandoned = true;
            // The opponent forfeits. Injected locally (the opponent is gone, nothing to keep in sync with).
            _m.Submit(new Command(_m.Tick, 1 - _local, CommandType.Surrender));
        }

        /// <summary>Tell the opponent we are leaving (after the match or on quit).</summary>
        public void SendBye() { if (_link.Connected) _link.Send(new[] { (byte)NetMsg.Bye }); }

        void SendFrame(int tick, List<Command> cmds)
        {
            int n = Math.Min(255, cmds.Count);
            var p = new byte[1 + 4 + 1 + n * 9];
            int pos = 0;
            p[pos++] = (byte)NetMsg.Frame;
            WriteInt(p, ref pos, tick);
            p[pos++] = (byte)n;
            for (int i = 0; i < n; i++)
            {
                p[pos++] = (byte)cmds[i].type;
                WriteInt(p, ref pos, cmds[i].a);
                WriteInt(p, ref pos, cmds[i].b);
            }
            _link.Send(p);
        }

        public static void WriteInt(byte[] p, ref int pos, int v)
        {
            p[pos++] = (byte)v; p[pos++] = (byte)(v >> 8); p[pos++] = (byte)(v >> 16); p[pos++] = (byte)(v >> 24);
        }

        public static int ReadInt(byte[] p, ref int pos)
        {
            int v = p[pos] | (p[pos + 1] << 8) | (p[pos + 2] << 16) | (p[pos + 3] << 24);
            pos += 4;
            return v;
        }
    }

    /// <summary>In-memory link pair with optional latency, for tests and local debugging.</summary>
    public sealed class LoopbackLink : IPacketLink
    {
        readonly Queue<KeyValuePair<int, byte[]>> _inbox = new Queue<KeyValuePair<int, byte[]>>();
        LoopbackLink _peer;
        public int Clock;          // advanced by the test
        public int LatencySteps;   // packets become readable this many clock steps later
        public bool Connected { get; set; }

        public static void Pair(out LoopbackLink a, out LoopbackLink b)
        {
            a = new LoopbackLink { Connected = true };
            b = new LoopbackLink { Connected = true };
            a._peer = b; b._peer = a;
        }

        public void Send(byte[] data) { if (Connected) _peer._inbox.Enqueue(new KeyValuePair<int, byte[]>(_peer.Clock + LatencySteps, data)); }

        public bool TryReceive(out byte[] data)
        {
            if (_inbox.Count > 0 && _inbox.Peek().Key <= Clock) { data = _inbox.Dequeue().Value; return true; }
            data = null;
            return false;
        }
    }
}
