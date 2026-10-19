using System.Collections.Generic;
using NUnit.Framework;
using ZooDefense.Core;

namespace ZooDefense.Tests
{
    /// <summary>Online lockstep: two devices that only exchange commands must end with the same state.</summary>
    public class LockstepTests
    {
        static MatchSetup Setup(int seed)
        {
            return new MatchSetup
            {
                seed = seed,
                players = new[]
                {
                    new PlayerSetup { name = "Host", deck = DefaultContent.DefaultDeck, sendDeck = DefaultContent.DefaultSendDeck },
                    new PlayerSetup { name = "Guest", deck = BotBrain.RandomDeck(DefaultContent.CreateDatabase(), seed), sendDeck = DefaultContent.DefaultSendDeck },
                },
            };
        }

        /// <summary>Runs two "devices" with a lossless link of the given latency, a bot driving each side.</summary>
        static void RunPair(int seed, int latency, int disconnectAtStep, out Match a, out Match b, out LockstepSync sa, out LockstepSync sb)
        {
            var content = DefaultContent.CreateDatabase();
            a = new Match(content, Setup(seed));
            b = new Match(content, Setup(seed));
            LoopbackLink la, lb;
            LoopbackLink.Pair(out la, out lb);
            la.LatencySteps = latency; lb.LatencySteps = latency;
            sa = new LockstepSync(a, 0, la);
            sb = new LockstepSync(b, 1, lb);
            var botA = new BotBrain(0, 2, seed + 1);
            var botB = new BotBrain(1, 2, seed + 2);
            var cmds = new List<Command>();
            for (int step = 0; step < 200000 && !(a.IsOver && b.IsOver); step++)
            {
                la.Clock = lb.Clock = step;
                if (step == disconnectAtStep) { la.Connected = false; lb.Connected = false; }
                double now = step * 0.05;
                sa.Pump(now, 5.0);
                sb.Pump(now, 5.0);
                if (!a.IsOver && sa.CanStep)
                {
                    cmds.Clear(); botA.Think(a, cmds); foreach (var c in cmds) sa.QueueLocal(c);
                    sa.BeforeStep(); a.Step();
                }
                if (!b.IsOver && sb.CanStep)
                {
                    cmds.Clear(); botB.Think(b, cmds); foreach (var c in cmds) sb.QueueLocal(c);
                    sb.BeforeStep(); b.Step();
                }
            }
        }

        [Test]
        public void TwoDevices_StayIdentical_WithLatency()
        {
            Match a, b; LockstepSync sa, sb;
            RunPair(77, 3, -1, out a, out b, out sa, out sb);
            Assert.IsTrue(a.IsOver && b.IsOver);
            Assert.IsFalse(sa.Desynced || sb.Desynced, "no desync");
            Assert.AreEqual(a.Winner, b.Winner);
            Assert.AreEqual(a.StateHash(), b.StateHash());
            Assert.AreEqual(a.Tick, b.Tick);
            // The command log of either device replays to the same result (server validation).
            Assert.IsTrue(ReplayValidator.Validate(DefaultContent.CreateDatabase(), MatchRecord.From(a)).valid);
        }

        [Test]
        public void LeavingRightAfterTheEnd_DoesNotChangeTheResult()
        {
            // The faster device finishes, says bye and disconnects; the slower one must still finish the same match.
            var content = DefaultContent.CreateDatabase();
            var a = new Match(content, Setup(79));
            var b = new Match(content, Setup(79));
            LoopbackLink la, lb;
            LoopbackLink.Pair(out la, out lb);
            la.LatencySteps = lb.LatencySteps = 2;
            var sa = new LockstepSync(a, 0, la);
            var sb = new LockstepSync(b, 1, lb);
            var botA = new BotBrain(0, 2, 80);
            var botB = new BotBrain(1, 2, 81);
            var cmds = new List<Command>();
            bool byeSent = false;
            for (int step = 0; step < 300000 && !b.IsOver; step++)
            {
                la.Clock = lb.Clock = step;
                double now = step * 0.05;
                sa.Pump(now, 5.0);
                sb.Pump(now, 5.0);
                if (!a.IsOver && sa.CanStep)
                {
                    cmds.Clear(); botA.Think(a, cmds); foreach (var c in cmds) sa.QueueLocal(c);
                    sa.BeforeStep(); a.Step();
                }
                if (a.IsOver && !byeSent) { sa.SendBye(); byeSent = true; }
                else if (byeSent) la.Connected = false;
                // Device B runs every other step: it is behind when A leaves.
                if (step % 2 == 0 && !b.IsOver && sb.CanStep)
                {
                    cmds.Clear(); botB.Think(b, cmds); foreach (var c in cmds) sb.QueueLocal(c);
                    sb.BeforeStep(); b.Step();
                }
            }
            Assert.IsTrue(a.IsOver && b.IsOver);
            Assert.IsFalse(sb.Abandoned, "no forfeit: B had every frame it needed");
            Assert.AreEqual(a.Winner, b.Winner);
            Assert.AreEqual(a.StateHash(), b.StateHash());
        }

        [Test]
        public void LostConnection_TheOtherSideWinsByForfeit()
        {
            Match a, b; LockstepSync sa, sb;
            RunPair(78, 2, 400, out a, out b, out sa, out sb);
            Assert.IsTrue(sa.Abandoned && sb.Abandoned);
            Assert.IsTrue(a.IsOver && b.IsOver);
            Assert.AreEqual(0, a.Winner, "host: the guest forfeits");
            Assert.AreEqual(1, b.Winner, "guest: the host forfeits");
        }
    }
}
