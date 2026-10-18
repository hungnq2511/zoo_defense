using System;
using System.Collections.Generic;
using UnityEngine;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>
    /// Where commands come from and go to. The prototype uses LocalBotTransport (bot in-process).
    /// For online PvP, add a RelayLockstepTransport: local commands are scheduled at Tick + inputDelay,
    /// sent to the peer through Unity Relay, and CanStep() returns false until the peer's input for
    /// the next tick has arrived. The Match itself does not change.
    /// </summary>
    public interface IMatchTransport
    {
        void Attach(Match match, int localPlayer);
        void SendLocal(Command command);
        /// <summary>Called right before each Step(); lets bots think or delivers remote commands.</summary>
        void BeforeStep();
        bool CanStep();
    }

    public sealed class LocalBotTransport : IMatchTransport
    {
        readonly BotBrain _bot;
        readonly List<Command> _buffer = new List<Command>();
        Match _match;

        public LocalBotTransport(BotBrain bot) { _bot = bot; }

        public void Attach(Match match, int localPlayer) { _match = match; }
        public void SendLocal(Command command) { _match.Submit(command); }
        public bool CanStep() { return true; }

        public void BeforeStep()
        {
            _buffer.Clear();
            _bot.Think(_match, _buffer);
            for (int i = 0; i < _buffer.Count; i++) _match.Submit(_buffer[i]);
        }
    }

    /// <summary>Drives a Match at a fixed tick rate from Unity's variable frame time.</summary>
    public sealed class MatchRunner
    {
        public readonly Match Match;
        public readonly int LocalPlayer;
        public bool Paused;
        public float TimeScale = 1f;
        /// <summary>0..1 progress towards the next tick; use it to interpolate positions.</summary>
        public float Alpha { get; private set; }
        public event Action<SimEvent> OnEvent;

        readonly IMatchTransport _transport;
        readonly float _dt;
        float _accumulator;

        public MatchRunner(Match match, int localPlayer, IMatchTransport transport)
        {
            Match = match;
            LocalPlayer = localPlayer;
            _transport = transport;
            _transport.Attach(match, localPlayer);
            _dt = 1f / match.Balance.tickRate;
        }

        public Board LocalBoard { get { return Match.Boards[LocalPlayer]; } }
        public Board RemoteBoard { get { return Match.Boards[1 - LocalPlayer]; } }

        public void Submit(CommandType type, int a = 0, int b = 0)
        {
            if (Match.IsOver) return;
            _transport.SendLocal(new Command(Match.Tick, LocalPlayer, type, a, b));
        }

        public void Update(float deltaTime)
        {
            if (Paused || Match.IsOver) return;
            _accumulator += Mathf.Min(deltaTime, 0.25f) * TimeScale;
            int guard = 0;
            while (_accumulator >= _dt && guard++ < 16)
            {
                if (!_transport.CanStep()) { _accumulator = Mathf.Min(_accumulator, _dt); break; }
                _transport.BeforeStep();
                Match.Step();
                _accumulator -= _dt;
                var events = Match.Events;
                if (OnEvent != null) for (int i = 0; i < events.Count; i++) OnEvent(events[i]);
                if (Match.IsOver) break;
            }
            Alpha = Mathf.Clamp01(_accumulator / _dt);
        }
    }
}
