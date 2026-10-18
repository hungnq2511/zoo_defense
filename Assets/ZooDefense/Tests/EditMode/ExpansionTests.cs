using System.Collections.Generic;
using NUnit.Framework;
using ZooDefense.Core;

namespace ZooDefense.Tests
{
    /// <summary>Expansion 1 animals / monsters and the element rules (triangle, resist, Light, wave element).</summary>
    public class ExpansionTests
    {
        static Match NewMatch(string[] deck, int seed = 3)
        {
            var c = DefaultContent.CreateDatabase();
            return new Match(c, new MatchSetup
            {
                seed = seed,
                players = new[]
                {
                    new PlayerSetup { name = "A", deck = deck, sendDeck = DefaultContent.DefaultSendDeck },
                    new PlayerSetup { name = "B", deck = deck, sendDeck = DefaultContent.DefaultSendDeck },
                },
            });
        }

        static void Do(Match m, CommandType t, int a = 0, int b = 0) { m.Submit(new Command(m.Tick, 0, t, a, b)); m.Step(); }

        /// <summary>Steps until board 0 lands an attack; returns the monster that was hit (or null).</summary>
        static EnemyInstance RunUntilHit(Match m, int maxTicks = 600)
        {
            for (int i = 0; i < maxTicks; i++)
            {
                m.Step();
                foreach (var e in m.Events)
                    if (e.type == SimEventType.UnitAttacked && e.board == 0)
                        foreach (var en in m.Boards[0].Enemies) if (en.uid == e.b) return en;
            }
            return null;
        }

        [Test]
        public void ElementRules_TriangleResistAndLight()
        {
            var b = new BalanceConfig();
            Assert.AreEqual(100 + b.elementBonusPercent, ElementRules.DamagePercent(b, Element.Fire, Element.Grass, false, false));
            Assert.AreEqual(100 - b.elementResistPercent, ElementRules.DamagePercent(b, Element.Fire, Element.Water, false, false));
            Assert.AreEqual(100, ElementRules.DamagePercent(b, Element.Fire, Element.Fire, false, false));
            Assert.AreEqual(100, ElementRules.DamagePercent(b, Element.Light, Element.Water, false, false), "Light is never resisted");
            Assert.AreEqual(100 + b.lightVsBigPercent, ElementRules.DamagePercent(b, Element.Light, Element.Fire, false, true), "Light vs bosses");
            Assert.AreEqual(100 + b.elementBonusPercent, ElementRules.DamagePercent(b, Element.Light, Element.Water, true, false), "always counter");
            Assert.AreEqual(Element.Grass, ElementRules.Beats(Element.Fire));
            Assert.AreEqual(Element.Water, ElementRules.WeakTo(Element.Fire));
        }

        [Test]
        public void ExpansionContent_IsInTheMonsterPools()
        {
            var c = DefaultContent.CreateDatabase();
            foreach (var id in new[] { "lava_salamander", "ember_boar", "forge_mole", "snow_seal", "coral_axolotl", "snap_shrimp", "nectar_bee", "sprout_elephant", "moon_moth", "aegis_ram" })
                Assert.IsNotNull(c.Unit(id), id);
            CollectionAssert.Contains(c.Balance.normalEnemyIds, "bandit_raccoon");
            CollectionAssert.Contains(c.Balance.eliteEnemyIds, "armored_scorpion");
            CollectionAssert.Contains(c.Balance.bossEnemyIds, "void_hermit_crab");
        }

        [Test]
        public void ForgeMole_BreaksArmor_And_Axolotl_Marks()
        {
            var m = NewMatch(new[] { "forge_mole" });
            Do(m, CommandType.SummonUnit, 0, 7);
            var hit = RunUntilHit(m);
            Assert.IsNotNull(hit);
            Assert.Greater(hit.shredTicks, 0, "armor break applied");

            m = NewMatch(new[] { "coral_axolotl" });
            Do(m, CommandType.SummonUnit, 0, 7);
            hit = RunUntilHit(m);
            Assert.IsNotNull(hit);
            Assert.Greater(hit.vulnTicks, 0, "vulnerable mark applied");
        }

        [Test]
        public void LavaSalamander_StacksBurn()
        {
            var m = NewMatch(new[] { "lava_salamander" });
            Do(m, CommandType.SummonUnit, 0, 7);
            var hit = RunUntilHit(m);
            Assert.IsNotNull(hit);
            Assert.Greater(hit.burnStacks, 0);
            Assert.Greater(hit.burnDamage, 0);
        }

        [Test]
        public void NectarBee_And_AegisRam_BuffTheNearestAttacker()
        {
            var m = NewMatch(new[] { "nectar_bee", "fire_fox" });
            var b = m.Boards[0];
            Do(m, CommandType.SummonUnit, 1, 6);
            var fox = b.Cells[6];
            int before = b.AttackInterval(fox);
            Do(m, CommandType.SummonUnit, 0, 7);
            for (int i = 0; i < 200 && fox.hasteTicks == 0; i++) m.Step();
            Assert.Greater(fox.hasteTicks, 0, "bee hastes the fox");
            Assert.Less(b.AttackInterval(fox), before, "faster attacks");

            m = NewMatch(new[] { "aegis_ram", "fire_fox" });
            b = m.Boards[0];
            m.Boards[0].GetType();
            Do(m, CommandType.SummonUnit, 1, 6);
            fox = b.Cells[6];
            int dmg = b.HitDamage(fox);
            Do(m, CommandType.SummonUnit, 0, 7);
            for (int i = 0; i < 200 && fox.guardTicks == 0; i++) m.Step();
            Assert.Greater(fox.guardCharges, 0, "ram shields the fox");
            Assert.Greater(b.HitDamage(fox), dmg, "shield blessing adds damage");
        }

        [Test]
        public void Waves_HaveAMainElement_AnnouncedOneWaveAhead()
        {
            int match = 0, total = 0;
            for (int seed = 1; seed <= 12; seed++)
            {
                var m = NewMatch(DefaultContent.DefaultDeck, seed);
                var announced = m.NextWaveElement;
                while (m.Wave < 1) m.Step();
                Assert.AreEqual(announced, m.WaveElement, "next-wave element is what comes");
                for (int i = 0; i < 300; i++) m.Step();
                foreach (var e in m.Boards[0].Enemies) { total++; if (e.element == m.WaveElement) match++; }
            }
            Assert.Greater(total, 0);
            Assert.Greater(match * 100 / total, 55, "most monsters share the wave element");
        }
    }
}
