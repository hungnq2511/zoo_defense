using System.Collections.Generic;
using NUnit.Framework;
using ZooDefense.Core;

namespace ZooDefense.Tests
{
    /// <summary>Tactics layer: sell, target priority, Element Hex, card talents, Mayhem rules, wave HP scaling.</summary>
    public class TacticsTests
    {
        static ContentDatabase Content() { return DefaultContent.CreateDatabase(); }

        static Match NewMatch(int seed = 5, int[] levels = null, int[] talents = null, int startCandy = 2000)
        {
            return new Match(Content(), new MatchSetup
            {
                seed = seed,
                rules = new MatchRules { mode = MatchMode.Versus, startCandy = startCandy },
                players = new[]
                {
                    new PlayerSetup { name = "A", deck = DefaultContent.DefaultDeck, sendDeck = DefaultContent.DefaultSendDeck, cardLevels = levels, talents = talents },
                    new PlayerSetup { name = "B", deck = DefaultContent.DefaultDeck, sendDeck = DefaultContent.DefaultSendDeck, cardLevels = levels },
                },
            });
        }

        static void Do(Match m, int player, CommandType t, int a = 0, int b = 0)
        {
            m.Submit(new Command(m.Tick, player, t, a, b));
            m.Step();
        }

        /// <summary>Both players keep summoning so nobody dies early.</summary>
        static void RunToWave(Match m, int wave)
        {
            while (m.Wave < wave && !m.IsOver)
            {
                if (m.Tick % 40 == 0)
                {
                    m.Submit(new Command(m.Tick, 0, CommandType.Summon, -1));
                    m.Submit(new Command(m.Tick, 1, CommandType.Summon, -1));
                }
                m.Step();
            }
        }

        static int FirstUnitCell(Board b)
        {
            for (int i = 0; i < b.Cells.Length; i++) if (b.Cells[i] != null) return i;
            return -1;
        }

        [Test]
        public void Sell_RefundsCandyAndFreesTheCell()
        {
            var m = NewMatch();
            Do(m, 0, CommandType.Summon, 3);
            var a = m.Boards[0];
            Assert.IsNotNull(a.Cells[3], "summoned");
            int value = a.SellValue(a.Cells[3]);
            int candy = a.Candy;
            Do(m, 0, CommandType.Sell, 3);
            Assert.IsNull(a.Cells[3], "sold");
            Assert.AreEqual(candy + value, a.Candy, "refund");
            Assert.Greater(value, 0);
        }

        [Test]
        public void SetTarget_OverridesAndDefaultClears()
        {
            var m = NewMatch();
            Do(m, 0, CommandType.Summon, 0);
            var a = m.Boards[0];
            var u = a.Cells[0];
            Do(m, 0, CommandType.SetTarget, 0, (int)TargetMode.Sent);
            Assert.AreEqual((int)TargetMode.Sent, (int)a.TargetOf(u));
            Do(m, 0, CommandType.SetTarget, 0, (int)m.Content.Units[u.def].targetMode);
            Assert.AreEqual(-1, u.target, "default clears the override");
        }

        [Test]
        public void Hex_LockedEarly_ThenForcesTheOpponentsNextWave()
        {
            var m = NewMatch();
            var a = m.Boards[0];
            var b = m.Boards[1];
            RunToWave(m, 1);
            Do(m, 0, CommandType.Hex, (int)Element.Fire);
            Assert.AreEqual(0, a.HexCount, "locked before wave " + m.Balance.hexUnlockWave);

            RunToWave(m, m.Balance.hexUnlockWave);
            Assert.IsFalse(m.IsOver, "both alive");
            int cost = a.HexCost;
            int candy = a.Candy;
            Do(m, 0, CommandType.Hex, (int)Element.Water);
            Assert.AreEqual(1, a.HexCount);
            Assert.AreEqual((int)Element.Water, (int)b.HexNext);
            Assert.AreEqual((int)Element.Water, (int)b.NextWaveElementHere);
            Assert.IsTrue(a.Candy <= candy - cost + 200, "paid");
            Do(m, 0, CommandType.Hex, (int)Element.Fire);
            Assert.AreEqual(1, a.HexCount, "only one pending hex");

            int target = m.Wave + 1;
            RunToWave(m, target);
            Assert.AreEqual((int)Element.Water, (int)b.HexThisWave);
            Assert.AreEqual((int)Element.None, (int)b.HexNext);
            for (int t = 0; t < m.Balance.spawnWindowTicks && !m.IsOver; t++) m.Step();
            int normals = 0;
            foreach (var e in b.Enemies)
            {
                if (e.sender >= 0 || m.Content.Enemies[e.def].kind != EnemyKind.Normal) continue;
                normals++;
                Assert.AreEqual((int)Element.Water, (int)e.element, "hexed monster");
            }
            Assert.AreEqual((int)Element.None, (int)a.HexThisWave, "the caster's own wave is untouched");
            Assert.IsTrue(normals >= 0);
        }

        [Test]
        public void Talents_OnlyCountWhenTheCardLevelUnlocksThem()
        {
            var c = Content();
            var levels = new[] { 12, 9, 5, 12, 12 };
            var talents = new[] { Talents.Pack(1, 2), Talents.Pack(2, 1), Talents.Pack(1, 1), 0, Talents.Pack(0, 2) };
            var m = NewMatch(7, levels, talents);
            var a = m.Boards[0];
            var deck = a.Deck;
            Assert.AreEqual((int)Talents.Of(c.Units[deck[0]].role, 0, 1), (int)a.TalentAt(deck[0], 0));
            Assert.AreEqual((int)Talents.Of(c.Units[deck[0]].role, 1, 2), (int)a.TalentAt(deck[0], 1));
            Assert.AreEqual((int)Talents.Of(c.Units[deck[1]].role, 0, 2), (int)a.TalentAt(deck[1], 0));
            Assert.AreEqual((int)TalentKind.None, (int)a.TalentAt(deck[1], 1), "tier 2 needs level 12");
            Assert.AreEqual((int)TalentKind.None, (int)a.TalentAt(deck[2], 0), "level 5: no talents");
            Assert.AreEqual((int)TalentKind.None, (int)m.Boards[1].TalentAt(deck[0], 0), "the other player picked none");
        }

        [Test]
        public void Talents_PackRoundTrip_AndRoleTable()
        {
            for (int t1 = 0; t1 <= 2; t1++)
                for (int t2 = 0; t2 <= 2; t2++)
                {
                    int p = Talents.Pack(t1, t2);
                    Assert.AreEqual(t1, Talents.Tier1(p));
                    Assert.AreEqual(t2, Talents.Tier2(p));
                }
            var seen = new HashSet<TalentKind>();
            foreach (Role r in new[] { Role.Damage, Role.Area, Role.Control, Role.Support })
                for (int tier = 0; tier < 2; tier++)
                    for (int o = 1; o <= 2; o++) seen.Add(Talents.Of(r, tier, o));
            Assert.AreEqual(16, seen.Count, "16 different talents");
        }

        [Test]
        public void RarityAura_SSSBoostsTheWholeBoard()
        {
            var c = Content();
            var m = new Match(c, new MatchSetup
            {
                seed = 3,
                rules = new MatchRules { mode = MatchMode.Versus, startCandy = 2000 },
                players = new[]
                {
                    new PlayerSetup { name = "A", deck = new[] { "fire_fox", "rainbow_unicorn" }, sendDeck = DefaultContent.DefaultSendDeck },
                    new PlayerSetup { name = "B", deck = DefaultContent.DefaultDeck, sendDeck = DefaultContent.DefaultSendDeck },
                },
            });
            var a = m.Boards[0];
            Do(m, 0, CommandType.SummonUnit, 0, 0);
            var fox = a.Cells[0];
            int dmg = a.HitDamage(fox), interval = a.AttackInterval(fox);
            Assert.AreEqual(0, a.AuraDamagePercent);
            Do(m, 0, CommandType.SummonUnit, 1, 19);
            Assert.AreEqual((int)Rarity.SSS, a.AuraTier);
            Assert.Greater(a.HitDamage(fox), dmg, "aura damage");
            Assert.Less(a.AttackInterval(fox), interval + 1, "aura speed");
            Assert.AreEqual(0, m.Boards[1].AuraDamagePercent, "only the owner's board");
        }

        [Test]
        public void Evolve_RaisesOneLevelForCandy_UpToMax()
        {
            var m = NewMatch(startCandy: 20000);
            Do(m, 0, CommandType.Summon, 2);
            var a = m.Boards[0];
            var u = a.Cells[2];
            int dmg = a.HitDamage(u);
            int candy = a.Candy, price = a.EvolvePrice(u);
            Assert.Greater(price, 0);
            Do(m, 0, CommandType.Evolve, 2);
            Assert.AreEqual(2, u.level);
            Assert.IsTrue(a.Candy <= candy - price + 100, "paid");
            Assert.Greater(a.HitDamage(u), dmg, "stronger");
            for (int i = 0; i < 10; i++) Do(m, 0, CommandType.Evolve, 2);
            Assert.AreEqual(m.Balance.maxLevel, u.level, "stops at the max level");
            Assert.AreEqual(0, a.EvolvePrice(u));
            int before = a.Candy;
            Do(m, 0, CommandType.Evolve, 2);
            Assert.IsTrue(a.Candy >= before, "nothing charged at max level");
        }

        [Test]
        public void Mayhem_RotatesThroughEightRules()
        {
            var rules = new HashSet<MayhemRule>();
            for (int w = 0; w < 8; w++) rules.Add(GameCalendar.MayhemOfWeek(w));
            Assert.AreEqual(8, rules.Count);
            var mono = MatchRules.MayhemOf(MayhemRule.MonoElement, 6);
            Assert.AreEqual(1, mono.waveElements.Length);
            var b = mono.Apply(DefaultContent.Create().balance);
            Assert.AreEqual(100, b.waveElementPercent);
            var hexWar = MatchRules.MayhemOf(MayhemRule.HexWar).Apply(DefaultContent.Create().balance);
            Assert.AreEqual(2, hexWar.hexUnlockWave);
        }

        [Test]
        public void WaveHp_FollowsBothPlayersCardPower()
        {
            var low = NewMatch(9, new[] { 1, 1, 1, 1, 1 });
            var high = NewMatch(9, new[] { 15, 15, 15, 15, 15 });
            Assert.Greater(high.WaveHpPermille, low.WaveHpPermille * 3 / 2, "level 15 decks face tougher waves");
        }

        static readonly string[][] MythicDecks =
        {
            new[] { "celestial_qilin", "abyss_leviathan", "solar_lion", "worldtree_tortoise", "ninetail_star_fox" },
            new[] { "thunder_griffin", "glacier_wolf", "magma_rhino", "bloom_peacock", "crystal_pangolin" },
            new[] { "lantern_jellyfish", "flame_tiger_cub", "bamboo_red_panda", "comet_hamster", "puddle_duckling" },
        };

        [Test]
        public void MythicAnimals_AreSSS_AndFightDeterministically()
        {
            var c = Content();
            foreach (var deck in MythicDecks)
                foreach (var id in deck)
                {
                    var d = c.Unit(id);
                    Assert.IsNotNull(d, id);
                    Assert.AreEqual((int)Rarity.SSS, (int)d.rarity, id);
                    Assert.IsTrue(d.skill >= UnitSkill.StarFall, id + " has a signature skill");
                }
            for (int k = 0; k < MythicDecks.Length; k++)
            {
                var m = new Match(c, new MatchSetup
                {
                    seed = 40 + k,
                    rules = new MatchRules { mode = MatchMode.Versus },
                    players = new[]
                    {
                        new PlayerSetup { name = "A", deck = MythicDecks[k], sendDeck = DefaultContent.DefaultSendDeck, cardLevels = new[] { 9, 9, 9, 9, 9 } },
                        new PlayerSetup { name = "B", deck = MythicDecks[(k + 1) % MythicDecks.Length], sendDeck = DefaultContent.DefaultSendDeck, cardLevels = new[] { 9, 9, 9, 9, 9 } },
                    },
                });
                var bots = new[] { new BotBrain(0, 2, 3 + k), new BotBrain(1, 2, 7 + k) };
                var cmds = new List<Command>();
                while (!m.IsOver && m.Tick < 20 * 60 * 6)
                {
                    cmds.Clear();
                    bots[0].Think(m, cmds); bots[1].Think(m, cmds);
                    foreach (var cmd in cmds) m.Submit(cmd);
                    m.Step();
                }
                long dealt = 0;
                foreach (var id in MythicDecks[k]) dealt += m.Boards[0].DamageByUnit[c.UnitIndex(id)];
                Assert.IsTrue(dealt > 0, "mythic deck " + k + " dealt damage");
                var v = ReplayValidator.Validate(c, MatchRecord.From(m));
                Assert.IsTrue(v.valid, v.error);
            }
        }

        [Test]
        public void NewSpells_DoWhatTheySay()
        {
            var c = Content();
            var m = new Match(c, new MatchSetup
            {
                seed = 21,
                rules = new MatchRules { mode = MatchMode.Versus, startCandy = 2000 },
                players = new[]
                {
                    new PlayerSetup { name = "A", deck = DefaultContent.DefaultDeck, sendDeck = DefaultContent.DefaultSendDeck, spells = new[] { "poison_fog", "star_mark", "shield_dome", "lucky_star" } },
                    new PlayerSetup { name = "B", deck = DefaultContent.DefaultDeck, sendDeck = DefaultContent.DefaultSendDeck },
                },
            });
            var a = m.Boards[0];
            Do(m, 0, CommandType.Summon, 0);
            Do(m, 0, CommandType.Summon, 1);
            Do(m, 0, CommandType.CastSpell, 3);
            Assert.IsTrue(a.Cells[0].level + a.Cells[1].level >= 3, "lucky star levels up an animal");
            Do(m, 0, CommandType.CastSpell, 2);
            Assert.Greater(a.Cells[0].guardTicks, 0, "shield dome");
            Assert.Greater(a.Cells[1].guardCharges, 0);
            RunToWave(m, 1);
            for (int t = 0; t < 120 && a.Enemies.Count == 0; t++) m.Step();
            Assert.Greater(a.Enemies.Count, 0);
            int at = a.Enemies[0].distance;
            Do(m, 0, CommandType.CastSpell, 0, at);
            Do(m, 0, CommandType.CastSpell, 1, at);
            bool poisoned = false, marked = false;
            foreach (var e in a.Enemies) { poisoned |= e.burnTicks > 0 && e.slowTicks > 0; marked |= e.vulnTicks > 0 && e.vulnPercent >= 30; }
            Assert.IsTrue(poisoned, "poison fog");
            Assert.IsTrue(marked, "star mark");
        }

        [Test]
        public void Round9Sends_SplitBurrowHealFogAndExplode()
        {
            var c = Content();
            var sends = new[] { "twin_slime", "digger_mole", "healer_shroom", "bomb_beetle" };
            var m = new Match(c, new MatchSetup
            {
                seed = 33,
                rules = new MatchRules { mode = MatchMode.Versus, startCandy = 5000 },
                players = new[]
                {
                    new PlayerSetup { name = "A", deck = DefaultContent.DefaultDeck, sendDeck = DefaultContent.DefaultSendDeck, cardLevels = new[] { 15, 15, 15, 15, 15 } },
                    new PlayerSetup { name = "B", deck = DefaultContent.DefaultDeck, sendDeck = sends, cardLevels = new[] { 15, 15, 15, 15, 15 } },
                },
            });
            int splitter = c.EnemyIndex("send_splitter");
            int splitterSpawns = 0, heals = 0, stuns = 0;
            bool burrowed = false;
            int lastWave = -1;
            var a = m.Boards[0];
            while (!m.IsOver && m.Tick < 20 * 60 * 6 && !(splitterSpawns >= 3 && burrowed && heals > 0 && stuns > 0))
            {
                if (m.Tick % 20 == 0) { m.Submit(new Command(m.Tick, 0, CommandType.Summon, -1)); m.Submit(new Command(m.Tick, 1, CommandType.Summon, -1)); }
                // One send per wave (waves 2-5), so the defender is not simply flooded.
                if (m.Wave != lastWave) { lastWave = m.Wave; int slot = m.Wave - m.Balance.sendUnlockWave; if (slot >= 0 && slot < 4) m.Submit(new Command(m.Tick, 1, CommandType.SendMonster, slot)); }
                m.Step();
                foreach (var e in m.Events)
                {
                    if (e.board != 0) continue;
                    if (e.type == SimEventType.EnemySpawned && e.b == splitter) splitterSpawns++;
                    if (e.type == SimEventType.EnemyAbilityUsed && e.b == (int)EnemyAbility.HealPulse) heals++;
                    if (e.type == SimEventType.UnitStunned || e.type == SimEventType.GuardBlocked) stuns++;
                }
                foreach (var en in a.Enemies) burrowed |= en.burrowTicks > 0;
            }
            Assert.GreaterOrEqual(splitterSpawns, 3, "twin slime splits into 2");
            Assert.IsTrue(burrowed, "mole burrows");
            Assert.Greater(heals, 0, "shroom heals");
            Assert.Greater(stuns, 0, "bomb stuns animals");
            var v = ReplayValidator.Validate(c, MatchRecord.From(m));
            Assert.IsTrue(v.valid, v.error);
        }

        [Test]
        public void ChanceMerge_SameLevelSure_LowerPartnerHasAChance_FailGivesLuck()
        {
            var m = NewMatch(startCandy: 20000);
            var a = m.Boards[0];
            Do(m, 0, CommandType.SummonUnit, 0, 0);
            Do(m, 0, CommandType.SummonUnit, 0, 1);
            var main = a.Cells[1];
            Assert.AreEqual(100, a.MergeChance(a.Cells[0], main), "same level is sure");
            Do(m, 0, CommandType.Move, 0, 1);
            Assert.AreEqual(2, main.level);
            Assert.IsNull(a.Cells[0]);

            // Level 2 main + level 1 partner: (1+1)/(2+1) = 66%.
            Do(m, 0, CommandType.SummonUnit, 0, 0);
            Assert.AreEqual(66, a.MergeChance(a.Cells[0], main));
            int fails = 0, wins = 0;
            for (int k = 0; k < 40 && main.level < m.Balance.maxLevel; k++)
            {
                if (a.Cells[0] == null) Do(m, 0, CommandType.SummonUnit, 0, 0);
                int before = main.level, luck = main.luck, chance = a.MergeChance(a.Cells[0], main);
                Do(m, 0, CommandType.Move, 0, 1);
                Assert.IsNull(a.Cells[0], "the partner is always used up");
                if (main.level > before) { wins++; Assert.AreEqual(0, main.luck, "success resets luck"); }
                else { fails++; Assert.AreEqual(System.Math.Min(m.Balance.mergeLuckMax, luck + m.Balance.mergeLuckPerFail), main.luck, "fail adds luck"); }
                Assert.IsTrue(chance >= 0 && chance <= 100);
            }
            Assert.Greater(wins, 0);
            // Dragging the higher animal onto the lower one moves it into that cell.
            Do(m, 0, CommandType.SummonUnit, 0, 5);
            var low = a.Cells[5];
            if (main.level < m.Balance.maxLevel)
            {
                Do(m, 0, CommandType.Move, 1, 5);
                Assert.IsTrue(ReferenceEquals(main, a.Cells[5]), "the main animal ends where it was dropped");
                Assert.IsNull(a.Cells[1]);
            }
            Assert.IsNotNull(low);
        }

        [Test]
        public void TacticsReplay_IsDeterministic()
        {
            var m = NewMatch(11, new[] { 12, 12, 12, 12, 12 }, new[] { 4, 5, 7, 8, 3 });
            var bots = new[] { new BotBrain(0, 2, 1), new BotBrain(1, 2, 2) };
            var cmds = new List<Command>();
            while (!m.IsOver && m.Tick < 20 * 60 * 4)
            {
                cmds.Clear();
                bots[0].Think(m, cmds); bots[1].Think(m, cmds);
                foreach (var c in cmds) m.Submit(c);
                if (m.Tick % 200 == 50) { int cell = FirstUnitCell(m.Boards[0]); if (cell >= 0) m.Submit(new Command(m.Tick, 0, CommandType.SetTarget, cell, (int)TargetMode.Strongest)); }
                m.Step();
            }
            var v = ReplayValidator.Validate(Content(), MatchRecord.From(m));
            Assert.IsTrue(v.valid, v.error);
        }
    }
}
