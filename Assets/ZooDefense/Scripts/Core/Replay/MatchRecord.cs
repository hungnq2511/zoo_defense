using System;
using System.Collections.Generic;

namespace ZooDefense.Core
{
    /// <summary>
    /// Everything needed to replay a match: setup + executed command log + expected outcome.
    /// The client uploads this after a ranked match; Cloud Code re-runs it with ReplayValidator
    /// before granting trophies and rewards (anti-cheat), and the same data powers replay viewing.
    /// </summary>
    [Serializable]
    public class MatchRecord
    {
        public int version = 1;
        public MatchSetup setup;
        public List<Command> commands = new List<Command>();
        public int finalTick;
        public int winner;
        public uint stateHash;

        public static MatchRecord From(Match m)
        {
            return new MatchRecord
            {
                setup = m.Setup,
                commands = new List<Command>(m.ExecutedCommands),
                finalTick = m.Tick,
                winner = m.IsOver ? m.Winner : -2,
                stateHash = m.StateHash(),
            };
        }
    }

    public static class ReplayValidator
    {
        public struct Result
        {
            public bool valid;
            public int winner;
            public uint stateHash;
            public string error;
        }

        public static Match Replay(ContentDatabase content, MatchRecord record)
        {
            var m = new Match(content, record.setup);
            for (int i = 0; i < record.commands.Count; i++) m.Submit(record.commands[i]);
            m.RunUntil(record.finalTick);
            return m;
        }

        public static Result Validate(ContentDatabase content, MatchRecord record)
        {
            try
            {
                var m = Replay(content, record);
                var r = new Result { winner = m.IsOver ? m.Winner : -2, stateHash = m.StateHash() };
                r.valid = r.winner == record.winner && r.stateHash == record.stateHash && m.Tick == record.finalTick;
                if (!r.valid) r.error = "Replay mismatch (winner " + r.winner + " vs " + record.winner + ", hash " + r.stateHash + " vs " + record.stateHash + ")";
                return r;
            }
            catch (Exception ex)
            {
                return new Result { valid = false, error = ex.Message };
            }
        }
    }
}
