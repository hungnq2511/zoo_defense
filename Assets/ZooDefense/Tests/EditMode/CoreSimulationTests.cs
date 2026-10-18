using System.Collections.Generic;
using NUnit.Framework;
using ZooDefense.Core;

namespace ZooDefense.Tests
{
    /// <summary>Run from Window > General > Test Runner > EditMode.</summary>
    public class CoreSimulationTests
    {
        static ContentDatabase Content() { return DefaultContent.CreateDatabase(); }

        static MatchSetup Setup(int seed, string[] deck0 = null, string[] deck1 = null)
        {
            return new MatchSetup
            {
                seed = seed,
                players = new[]
                {
                    new PlayerSetup { name = "A", deck = deck0 ?? DefaultContent.DefaultDeck, sendDeck = DefaultContent.DefaultSendDeck },
                    new PlayerSetup { name = "B", deck = deck1 ?? DefaultContent.DefaultDeck, sendDeck = DefaultContent.DefaultSendDeck },
                },
            };
        }

        static Match RunBots(ContentDatabase content, int seed)
        {
            var m = new Match(content, Setup(seed, BotBrain.RandomDeck(content, seed * 3), BotBrain.RandomDeck(content, seed * 7)));
            var bots = new[] { new BotBrain(0, 1, seed + 1), new BotBrain(1, 2, seed + 2) };
            var cmds = new List<Command>();
            while (!m.IsOver && m.Tick < 20 * 60 * 30)
            {
                cmds.Clear();
                bots[0].Think(m, cmds);
                bots[1].Think(m, cmds);
                foreach (var c in cmds) m.Submit(c);
                m.Step();
            }
            return m;
        }

        [Test]
        public void DefaultContent_IsValid()
        {
            var c = Content();
            Assert.AreEqual(20, c.Units.Count);
            Assert.AreEqual(7, c.Sends.Count);   // 6 sends + the boss-duel remnant
        }

        [Test]
        public void SameSeedAndCommands_GiveSameResult()
        {
            var c = Content();
            var a = RunBots(c, 4242);
            var b = RunBots(c, 4242);
            Assert.IsTrue(a.IsOver);
            Assert.AreEqual(a.StateHash(), b.StateHash());
            Assert.AreEqual(a.Winner, b.Winner);
        }

        [Test]
        public void Replay_ValidatesAndDetectsTampering()
        {
            var c = Content();
            var m = RunBots(c, 99);
            var record = MatchRecord.From(m);
            Assert.IsTrue(ReplayValidator.Validate(c, record).valid);

            // Remove a purchase (a command that always changes the state; a rejected command would not).
            var tampered = MatchRecord.From(m);
            int buy = tampered.commands.FindIndex(x => x.type == CommandType.BuyCard || x.type == CommandType.Place || x.type == CommandType.Summon);
            tampered.commands.RemoveAt(buy);
            Assert.IsFalse(ReplayValidator.Validate(c, tampered).valid);
        }

        [Test]
        public void BuyingACard_SpendsCandyAndPlacesUnit()
        {
            var c = Content();
            var m = new Match(c, Setup(1));
            m.Submit(new Command(0, 0, CommandType.BuyCard, 0));
            m.Step();
            Assert.AreEqual(c.Balance.startCandy - c.Balance.cardBasePrice, m.Boards[0].Candy);
            Assert.AreEqual(c.Balance.gridCols * c.Balance.gridRows - 1, m.Boards[0].CountFreeCells());
        }

        [Test]
        public void TwoSameUnits_MergeIntoNextLevel()
        {
            var c = Content();
            var oneUnitDeck = new[] { "fire_fox" };
            var m = new Match(c, Setup(2, oneUnitDeck, oneUnitDeck));
            m.Submit(new Command(0, 0, CommandType.BuyCard, 0));
            m.Submit(new Command(0, 0, CommandType.BuyCard, 1));
            m.Step();
            int from = -1, to = -1;
            for (int i = 0; i < m.Boards[0].Cells.Length; i++)
                if (m.Boards[0].Cells[i] != null) { if (from < 0) from = i; else to = i; }
            Assert.GreaterOrEqual(to, 0, "two units should be on the board");

            m.Submit(new Command(m.Tick, 0, CommandType.Move, from, to));
            m.Step();
            Assert.IsNull(m.Boards[0].Cells[from]);
            Assert.AreEqual(2, m.Boards[0].Cells[to].level);
        }

        [Test]
        public void SendMonster_CostsCandyAndRaisesIncome()
        {
            var m = new Match(Content(), Setup(3));
            var a = m.Boards[0];
            int candy = a.Candy, cost = a.SendCost(0), income = a.SendIncome(0);
            m.Submit(new Command(0, 0, CommandType.SendMonster, 0));
            m.Step();
            Assert.AreEqual(1, m.Boards[1].IncomingSends.Count, "travelling to the opponent");
            Assert.AreEqual(candy - cost, a.Candy);
            Assert.AreEqual(income, a.Income);
            // It arrives live (not at the next wave) after the travel time.
            m.RunUntil(m.Tick + m.Balance.sendTravelTicks + 2);
            Assert.AreEqual(0, m.Boards[1].IncomingSends.Count, "arrived");
        }

        [Test]
        public void Surrender_EndsMatch()
        {
            var m = new Match(Content(), Setup(4));
            m.Submit(new Command(0, 0, CommandType.Surrender));
            m.Step();
            Assert.IsTrue(m.IsOver);
            Assert.AreEqual(1, m.Winner);
        }

        [Test]
        public void EmptyBoard_LosesWithinAFewWaves()
        {
            var m = new Match(Content(), Setup(5));
            m.RunUntil(20 * 60 * 5);
            Assert.IsTrue(m.IsOver);
            Assert.AreEqual(-1, m.Winner, "both idle players leak equally -> draw");
        }
    }
}
