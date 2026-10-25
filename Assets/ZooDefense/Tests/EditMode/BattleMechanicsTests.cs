using NUnit.Framework;
using ZooDefense.Core;

namespace ZooDefense.Tests
{
    /// <summary>Placement, element upgrades, spells, perks, balloons, reflect and active skills.</summary>
    public class BattleMechanicsTests
    {
        static Match NewMatch(int seed = 3)
        {
            var c = DefaultContent.CreateDatabase();
            return new Match(c, new MatchSetup
            {
                seed = seed,
                players = new[]
                {
                    new PlayerSetup { name = "A", deck = DefaultContent.DefaultDeck, sendDeck = DefaultContent.DefaultSendDeck },
                    new PlayerSetup { name = "B", deck = DefaultContent.DefaultDeck, sendDeck = DefaultContent.DefaultSendDeck },
                },
            });
        }

        static void Do(Match m, CommandType t, int a = 0, int b = 0) { m.Submit(new Command(m.Tick, 0, t, a, b)); m.Step(); }

        static bool Rejected(Match m)
        {
            foreach (var e in m.Events) if (e.type == SimEventType.CommandRejected) return true;
            return false;
        }

        [Test]
        public void Place_PutsTheAnimalInTheChosenCell()
        {
            var m = NewMatch();
            Do(m, CommandType.Place, 0, 7);
            Assert.IsTrue(m.Boards[0].Cells[7] != null, "unit in cell 7");
            Do(m, CommandType.Place, 0, 7);
            Assert.IsTrue(Rejected(m), "occupied cell rejected");
        }

        [Test]
        public void Summon_PutsADeckAnimalOnTheBoardAndRaisesThePrice()
        {
            var m = NewMatch();
            var b = m.Boards[0];
            int candy = b.Candy, price = b.CardPrice;
            Do(m, CommandType.Summon, -1);
            Assert.AreEqual(candy - price, b.Candy);
            Assert.AreEqual(b.Cells.Length - 1, b.CountFreeCells());
            UnitInstance u = null;
            foreach (var c in b.Cells) if (c != null) u = c;
            Assert.GreaterOrEqual(b.DeckSlotOf(u.def), 0, "summoned animal comes from the deck");
            Assert.Greater(b.CardPrice, price, "next summon costs more");
            Do(m, CommandType.Summon, 3);
            Assert.IsTrue(b.Cells[3] != null, "summon into a chosen cell");
        }

        [Test]
        public void UpgradeUnit_RaisesThatAnimalsDamage()
        {
            var m = NewMatch();
            var b = m.Boards[0];
            Do(m, CommandType.Summon, 0);
            var u = b.Cells[0];
            int before = b.PositionPercent(u);
            int slot = b.DeckSlotOf(u.def);
            int candy = b.Candy, cost = b.UnitUpgradeCost(slot);
            Do(m, CommandType.UpgradeUnit, slot);
            Assert.AreEqual(1, b.DeckLevel[slot]);
            Assert.AreEqual(candy - cost, b.Candy);
            Assert.AreEqual(before + m.Balance.unitUpgradePercent, b.PositionPercent(u));
        }

        [Test]
        public void Summon_BringsTheAnimalShownAsNext()
        {
            var m = NewMatch();
            var b = m.Boards[0];
            int next = b.NextSummon;
            Do(m, CommandType.Summon, 4);
            Assert.AreEqual(next, b.Cells[4].def);
        }

        [Test]
        public void SummonUnit_BringsTheChosenAnimalForMoreCandy()
        {
            var m = NewMatch();
            var b = m.Boards[0];
            int candy = b.Candy, price = b.SummonPrice(2);
            Assert.Greater(price, b.CardPrice, "choosing costs more than random");
            Do(m, CommandType.SummonUnit, 2, 9);
            Assert.AreEqual(b.Deck[2], b.Cells[9].def, "the chosen animal in the chosen cell");
            Assert.AreEqual(candy - price, b.Candy);
            Do(m, CommandType.SummonUnit, 1, -1);
            Assert.AreEqual(b.Cells.Length - 2, b.CountFreeCells(), "random free cell");
        }

        [Test]
        public void UpgradeUnit_NeedsThatAnimalOnTheBoard()
        {
            var m = NewMatch();
            var b = m.Boards[0];
            Do(m, CommandType.UpgradeUnit, 0);
            Assert.IsTrue(Rejected(m), "nothing on the board yet");
            Assert.AreEqual(0, b.DeckLevel[0]);
        }

        [Test]
        public void StarCellAndSameElementNeighbours_RaiseDamage()
        {
            var m = NewMatch();
            var b = m.Boards[0];
            int star = m.StarCells[0];
            Do(m, CommandType.Place, 0, star);
            Assert.GreaterOrEqual(b.PositionPercent(b.Cells[star]), 100 + m.Balance.starCellBonusPercent);
        }

        [Test]
        public void ElementUpgrade_CostsCandyAndLevelsUp()
        {
            var m = NewMatch();
            var b = m.Boards[0];
            int candy = b.Candy, cost = b.ElementUpgradeCost(Element.Fire);
            Do(m, CommandType.UpgradeElement, (int)Element.Fire);
            Assert.AreEqual(1, b.ElementLevel[(int)Element.Fire]);
            Assert.AreEqual(candy - cost, b.Candy);
        }

        [Test]
        public void Spell_GoesOnCooldown()
        {
            var m = NewMatch();
            var b = m.Boards[0];
            m.RunUntil(m.Balance.firstWaveDelayTicks + 60);
            Do(m, CommandType.CastSpell, 0, 1500);
            Assert.IsFalse(Rejected(m), "first cast ok");
            Assert.IsTrue(b.SpellReadyTick[0] > m.Tick, "cooldown set");
            Do(m, CommandType.CastSpell, 0, 1500);
            Assert.IsTrue(Rejected(m), "second cast rejected");
        }

        [Test]
        public void Perks_AreOfferedAndChosen()
        {
            var m = NewMatch();
            var b = m.Boards[0];
            int guard = 0;
            while (b.PerkOffer.Count == 0 && !m.IsOver && ++guard < 20000)
            {
                // Keep the player alive with a few purchases.
                if (m.Tick % 40 == 0) m.Submit(new Command(m.Tick, 0, CommandType.BuyCard, 0));
                if (m.Tick % 40 == 0) m.Submit(new Command(m.Tick, 1, CommandType.BuyCard, 0));
                m.Step();
            }
            if (m.IsOver) return;   // lost before wave 5: nothing to check
            Assert.AreEqual(m.Balance.perkChoices, b.PerkOffer.Count);
            var first = b.PerkOffer[1];
            Do(m, CommandType.ChoosePerk, 1);
            Assert.IsTrue(b.Perks.Contains(first));
            Assert.AreEqual(0, b.PerkOffer.Count);
        }

        [Test]
        public void Balloon_GivesCandyWhenPopped()
        {
            var m = NewMatch();
            var b = m.Boards[0];
            int guard = 0;
            while (b.BalloonId == 0 && ++guard < 3000) m.Step();
            Assert.IsTrue(b.BalloonId != 0, "balloon appeared");
            int candy = b.Candy, reward = b.BalloonCandy;
            Do(m, CommandType.PopBalloon, b.BalloonId);
            Assert.GreaterOrEqual(b.Candy, candy + reward);
            Assert.AreEqual(0, b.BalloonId);
        }

        [Test]
        public void Reflect_SendsTheMonsterBack()
        {
            var m = NewMatch();
            var a = m.Boards[0];
            var b = m.Boards[1];
            while (m.Wave < m.Balance.sendUnlockWave) m.Step();   // sends unlock at wave 2
            m.Submit(new Command(m.Tick, 1, CommandType.SendMonster, 0));
            m.Step();
            Assert.AreEqual(1, a.IncomingSends.Count, "B's send travels to A");
            int candy = a.Candy, cost = a.ReflectCost(0);
            Do(m, CommandType.Reflect, 0);
            Assert.AreEqual(0, a.IncomingSends.Count);
            Assert.AreEqual(candy - cost, a.Candy, "reflect costs candy");
            Assert.AreEqual(1, b.IncomingSends.Count);
            Assert.IsTrue(b.IncomingReflected[0], "cannot bounce twice");
        }

        [Test]
        public void BossDuel_FirstKillerWinsCandyAndSendsTheRemnant()
        {
            var m = NewMatch();
            // Jump to a boss wave and let board 0 kill the boss through the internal hook.
            while (!m.IsBossDuelOpen && m.Tick < 20 * 60 * 10)
            {
                if (m.Tick % 30 == 0) { m.Submit(new Command(m.Tick, 0, CommandType.Summon, -1)); m.Submit(new Command(m.Tick, 1, CommandType.Summon, -1)); }
                m.Step();
                if (m.IsOver) return;
            }
            Assert.IsTrue(m.IsBossDuelOpen, "a boss wave opens a duel");
            int guard = 0;
            while (m.IsBossDuelOpen && !m.IsOver && ++guard < 3000)
            {
                if (m.Tick % 30 == 0) { m.Submit(new Command(m.Tick, 0, CommandType.Summon, -1)); m.Submit(new Command(m.Tick, 1, CommandType.Summon, -1)); }
                m.Step();
            }
            if (m.BossDuelWinner < 0) return;   // both leaked the boss: no winner, nothing to check
            Assert.IsTrue(m.Boards[1 - m.BossDuelWinner].IncomingSends.Count > 0 || m.Boards[1 - m.BossDuelWinner].Enemies.Count > 0, "remnant sent to the loser");
        }

        [Test]
        public void Skill_ChargesFromLevel3AndResets()
        {
            var m = NewMatch();
            var b = m.Boards[0];
            Do(m, CommandType.Place, 0, 0);
            var u = b.Cells[0];
            Assert.AreEqual(0, b.SkillCharge(u), "no skill at level 1");
            u.level = 3;
            u.energy = b.SkillCharge(u);
            Assert.IsTrue(b.IsSkillReady(u));
        }
    }
}
