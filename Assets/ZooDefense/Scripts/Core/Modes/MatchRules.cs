using System;
using System.Collections.Generic;

namespace ZooDefense.Core
{
    public enum MatchMode
    {
        Versus = 0,      // ranked / practice 1v1 (default)
        Daily = 1,       // solo: same seed + deck for everyone today, score = wave reached
        Endless = 2,     // solo: waves forever, weekly best
        Mayhem = 3,      // 1v1 with the weekly special rule
        Campaign = 4,    // solo: survive N waves with the level's rules, 1-3 stars
        Raid = 5,        // co-op: both boards fight one shared giant boss
        Tournament = 6,  // 1v1 knockout, card levels normalised
    }

    /// <summary>Weekly special rule of the Mayhem arena.</summary>
    public enum MayhemRule
    {
        None = 0,
        Mirror = 1,        // both players get the same random deck (set up by the client)
        ElementChaos = 2,  // counters x2, resisted x0.5, every wave monster has the wave element
        SendStorm = 3,     // sends cost half, income doubled
        StarHop = 4,       // star cells move every wave and give +50%
    }

    /// <summary>
    /// Per-match rule set. Null (or mode Versus with defaults) = the normal ranked rules. The rules are part of the
    /// MatchSetup, so replays and server validation apply exactly the same rules.
    /// </summary>
    [Serializable]
    public class MatchRules
    {
        public MatchMode mode;
        /// <summary>Only board 0 plays (PvE); board 1 is idle and can never lose.</summary>
        public bool solo;
        /// <summary>Both boards are one team (raid): no sends, Assist commands, team wins or loses together.</summary>
        public bool coop;
        /// <summary>> 0: surviving this many waves wins (campaign).</summary>
        public int winWave;
        public int startHearts;
        public int startCandy;
        /// <summary>Monster HP scale (100 = normal).</summary>
        public int hpPercent = 100;
        /// <summary>> 0: HP growth per wave (PvE has no sends, so its waves must ramp up faster).</summary>
        public int hpGrowthPercent;
        /// <summary>Main element of each wave, cycled (values of Element). Empty = random as usual.</summary>
        public int[] waveElements;
        public int bossEveryNWaves;
        public string[] bossIds;
        public string[] normalEnemyIds;
        public bool noSuddenDeath;
        public MayhemRule mayhem;
        /// <summary>Raid: the wave the giant boss arrives, its id and its HP (in % of a normal boss of that wave).</summary>
        public int raidBossWave = 3;
        public string raidBossId = "clockwork_gorilla";
        public int raidHpPercent = 1600;
        /// <summary>Raid: speed of the giant boss in % of its normal speed.</summary>
        public int raidSpeedPercent = 35;

        public bool IsSolo { get { return solo; } }

        /// <summary>A copy of the balance with this rule set applied (the shared content is never modified).</summary>
        public BalanceConfig Apply(BalanceConfig src)
        {
            var b = src.Clone();
            if (startHearts > 0) b.startHearts = startHearts;
            if (startCandy > 0) b.startCandy = startCandy;
            if (hpPercent > 0 && hpPercent != 100) b.waveHpStartPermille = Math.Max(100, b.waveHpStartPermille * hpPercent / 100);
            if (hpGrowthPercent > 0) b.hpGrowthPercentPerWave = hpGrowthPercent;
            if (bossEveryNWaves > 0) b.bossEveryNWaves = bossEveryNWaves;
            if (bossIds != null && bossIds.Length > 0) b.bossEnemyIds = bossIds;
            if (normalEnemyIds != null && normalEnemyIds.Length > 0) b.normalEnemyIds = normalEnemyIds;
            if (noSuddenDeath) b.suddenDeathWave = 100000;
            switch (mayhem)
            {
                case MayhemRule.ElementChaos:
                    b.elementBonusPercent = 100;
                    b.elementResistPercent = 50;
                    b.waveElementPercent = 100;
                    break;
                case MayhemRule.SendStorm:
                    b.sendCostPercent = 50;
                    b.sendIncomePercent = 200;
                    break;
                case MayhemRule.StarHop:
                    b.starCellMoveEveryWaves = 1;
                    b.starCellBonusPercent = 50;
                    break;
            }
            return b;
        }

        // PvE numbers tuned with bot runs: daily ~ wave 18 (6-7 min), endless ~ wave 26 (8-9 min), raid ~60% wins.
        public static MatchRules Daily() { return new MatchRules { mode = MatchMode.Daily, solo = true, noSuddenDeath = true, hpPercent = 160, hpGrowthPercent = 20 }; }
        public static MatchRules Endless() { return new MatchRules { mode = MatchMode.Endless, solo = true, noSuddenDeath = true, hpPercent = 140, hpGrowthPercent = 18 }; }
        public static MatchRules Raid() { return new MatchRules { mode = MatchMode.Raid, coop = true, noSuddenDeath = true, raidHpPercent = 4000, hpGrowthPercent = 11 }; }
        public static MatchRules Tournament() { return new MatchRules { mode = MatchMode.Tournament }; }
        public static MatchRules MayhemOf(MayhemRule rule) { return new MatchRules { mode = MatchMode.Mayhem, mayhem = rule }; }
    }

    /// <summary>Calendar helpers shared by the client and the server (all in UTC days / weeks).</summary>
    public static class GameCalendar
    {
        public const long Day = 86400;
        /// <summary>Weeks start on Monday 00:00 UTC (1970-01-05 was a Monday).</summary>
        public static int WeekIndex(long unixSeconds) { return (int)((unixSeconds - 4 * Day) / (7 * Day)); }
        public static int DayIndex(long unixSeconds) { return (int)(unixSeconds / Day); }
        public static long WeekEnd(long unixSeconds) { return (WeekIndex(unixSeconds) + 1) * 7 * Day + 4 * Day; }
        public static long DayEnd(long unixSeconds) { return (DayIndex(unixSeconds) + 1) * Day; }
        /// <summary>Day of the week, 0 = Monday ... 6 = Sunday.</summary>
        public static int Weekday(long unixSeconds) { return (int)(((unixSeconds - 4 * Day) / Day) % 7); }
        /// <summary>Mayhem arena is open Friday to Sunday.</summary>
        public static bool IsWeekend(long unixSeconds) { return Weekday(unixSeconds) >= 4; }

        public static MayhemRule MayhemOfWeek(int week)
        {
            switch (((week % 4) + 4) % 4) { case 0: return MayhemRule.Mirror; case 1: return MayhemRule.ElementChaos; case 2: return MayhemRule.SendStorm; default: return MayhemRule.StarHop; }
        }

        /// <summary>Ranked seasons last 4 weeks.</summary>
        public static int SeasonIndex(long unixSeconds) { return Math.Max(0, WeekIndex(unixSeconds)) / 4; }
        public static long SeasonEnd(long unixSeconds) { return ((long)(SeasonIndex(unixSeconds) + 1) * 4 * 7 + 4) * Day; }
    }

    /// <summary>Today's challenge: one seed, one deck and one card level for every player in the world.</summary>
    public static class DailyChallenge
    {
        public const int CardLevel = 8;
        public static readonly int[] MilestoneWaves = { 5, 10, 15, 20 };

        public static int Seed(int day) { return day * 7919 + 104729; }

        public static string[] Deck(ContentDatabase content, int day)
        {
            // 2 elements + 1 Light so the counter lesson of the day is clear.
            var rng = new DetRandom(Seed(day) ^ 0x6d2b79f5);
            var elements = new[] { Element.Fire, Element.Water, Element.Grass };
            var a = elements[rng.Next(3)];
            var b = elements[(Array.IndexOf(elements, a) + 1 + rng.Next(2)) % 3];
            var deck = new List<string>();
            Pick(content, ref rng, deck, a, 2);
            Pick(content, ref rng, deck, b, 2);
            Pick(content, ref rng, deck, Element.Light, 1);
            return deck.ToArray();
        }

        static void Pick(ContentDatabase content, ref DetRandom rng, List<string> deck, Element e, int count)
        {
            var pool = new List<string>();
            foreach (var u in content.Units)
                if (u.element == e && u.damage > 0 && !deck.Contains(u.id)) pool.Add(u.id);
            for (int i = 0; i < count && pool.Count > 0; i++) { int k = rng.Next(pool.Count); deck.Add(pool[k]); pool.RemoveAt(k); }
        }

        public static int[] Levels(int count) { var l = new int[count]; for (int i = 0; i < count; i++) l[i] = CardLevel; return l; }

        /// <summary>Main element of each wave: the same pattern for everyone, reshuffled every day.</summary>
        public static int[] WaveElements(int day)
        {
            var rng = new DetRandom(Seed(day) ^ 0x1b873593);
            var list = new int[12];
            for (int i = 0; i < list.Length; i++) list[i] = 1 + rng.Next(3);
            return list;
        }
    }

    /// <summary>One campaign level: rules + goal. 5 regions x 10 levels, built from data so new levels are cheap.</summary>
    public sealed class CampaignLevel
    {
        public int index;          // 0..49
        public int region;         // 0..4 (= arena 1..5)
        public int number;         // 1..10 inside the region
        public int winWave;
        public int hearts;
        public int hpPercent;
        public Element onlyElement;   // None = any animal
        public int[] waveElements;
        public string bossId;          // boss of the level (region theme)
        public int bossEvery;
        public string ruleKey;         // localisation key of the special rule
        public int gold, gems;         // first-clear reward
    }

    public static class Campaign
    {
        public const int Regions = 5, LevelsPerRegion = 10;
        public static int Count { get { return Regions * LevelsPerRegion; } }

        static readonly string[] RegionBoss = { "boss_summoner", "clockwork_gorilla", "boss_stunner", "void_hermit_crab", "boss_healer" };
        static readonly Element[] RegionElement = { Element.Grass, Element.Fire, Element.Fire, Element.Water, Element.Light };

        public static CampaignLevel Get(int index)
        {
            index = Math.Max(0, Math.Min(Count - 1, index));
            int region = index / LevelsPerRegion, n = index % LevelsPerRegion + 1;
            var rng = new DetRandom(index * 2654435 + 977);
            var lv = new CampaignLevel
            {
                index = index, region = region, number = n,
                winWave = 6 + region * 2 + (n >= 10 ? 4 : n / 3),
                hearts = n == 10 ? 8 : 10,
                hpPercent = (region == 0 ? 75 : 115 + region * 12) + n * 5,   // region 1 is the on-ramp for level-1 cards
                onlyElement = Element.None,
                bossId = RegionBoss[region],
                bossEvery = n == 10 ? 4 : 5,
                gold = 60 + 20 * region + 6 * n,
                gems = n == 10 ? 30 : (n % 5 == 0 ? 10 : 0),
                ruleKey = "campaign.rule.none",
            };
            // Every level has one twist so the region teaches something.
            switch (n % 5)
            {
                case 1:   // the region's element dominates: bring its counter
                {
                    var e = (int)RegionElement[region] == (int)Element.Light ? (Element)(1 + rng.Next(3)) : RegionElement[region];
                    lv.waveElements = new[] { (int)e };
                    lv.ruleKey = "campaign.rule.element_wave";
                    break;
                }
                case 2:   // only one element allowed in your team
                    lv.onlyElement = (Element)(1 + (index % 4));
                    lv.ruleKey = "campaign.rule.only_element";
                    break;
                case 3:   // alternating elements: mix your team
                {
                    int a = 1 + rng.Next(3), b = 1 + (a % 3);
                    lv.waveElements = new[] { a, a, b, b };
                    lv.ruleKey = "campaign.rule.alternating";
                    break;
                }
                case 4:   // few hearts
                    lv.hearts = 5;
                    lv.hpPercent -= 10;
                    lv.ruleKey = "campaign.rule.few_hearts";
                    break;
                default:  // boss level
                    lv.bossEvery = 3;
                    lv.ruleKey = "campaign.rule.boss_rush";
                    break;
            }
            return lv;
        }

        public static MatchRules Rules(CampaignLevel lv)
        {
            return new MatchRules
            {
                mode = MatchMode.Campaign, solo = true, winWave = lv.winWave, startHearts = lv.hearts,
                hpPercent = lv.hpPercent, hpGrowthPercent = 14, waveElements = lv.waveElements, bossEveryNWaves = lv.bossEvery,
                bossIds = new[] { lv.bossId }, noSuddenDeath = true,
            };
        }

        /// <summary>Stars: 1 = cleared, 2 = lost at most a third of the hearts, 3 = lost none.</summary>
        public static int Stars(CampaignLevel lv, bool cleared, int heartsLeft)
        {
            if (!cleared) return 0;
            if (heartsLeft >= lv.hearts) return 3;
            if (heartsLeft * 3 >= lv.hearts * 2) return 2;
            return 1;
        }
    }

    /// <summary>Weekend tournament: 3 knockout rounds against bots of growing skill, card levels set to 11.</summary>
    public static class TournamentRules
    {
        public const int Rounds = 3, CardLevel = 11;
        public static int BotDifficulty(int round) { return round == 0 ? 1 : 2; }
        public static int[] Levels(int count) { var l = new int[count]; for (int i = 0; i < count; i++) l[i] = CardLevel; return l; }
    }

    /// <summary>Live-ops calendar: what the current week celebrates (driven by Remote Config later, code default now).</summary>
    public static class LiveOps
    {
        public enum EventKind { ElementWeek = 0, BossWeek = 1, CandyFestival = 2 }

        public struct WeekEvent
        {
            public EventKind kind;
            public Element element;    // ElementWeek
            public string bossId;      // BossWeek
            public int week;
        }

        public static WeekEvent Of(int week)
        {
            int k = ((week % 6) + 6) % 6;
            var e = new WeekEvent { week = week };
            switch (k)
            {
                case 0: e.kind = EventKind.ElementWeek; e.element = Element.Fire; break;
                case 1: e.kind = EventKind.BossWeek; e.bossId = "clockwork_gorilla"; break;
                case 2: e.kind = EventKind.ElementWeek; e.element = Element.Water; break;
                case 3: e.kind = EventKind.CandyFestival; break;
                case 4: e.kind = EventKind.ElementWeek; e.element = Element.Grass; break;
                default: e.kind = EventKind.BossWeek; e.bossId = "void_hermit_crab"; break;
            }
            return e;
        }

        /// <summary>The event's twist on ranked and practice matches.</summary>
        public static void ApplyTo(WeekEvent ev, MatchRules rules)
        {
            if (ev.kind == EventKind.ElementWeek) rules.waveElements = new[] { (int)ev.element };
            else if (ev.kind == EventKind.BossWeek) rules.bossIds = new[] { ev.bossId };
            else if (ev.kind == EventKind.CandyFestival) rules.startCandy = 160;
        }
    }
}
