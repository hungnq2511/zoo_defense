using System;
using System.Collections.Generic;

namespace ZooDefense.Core
{
    // Phase 1-3 meta: bestiary, achievements + titles, unlockable emotes, saved decks, game-mode records
    // (daily, endless, campaign, mayhem, raid, tournament) and ranked seasons. Same pure C# as the rest of the
    // economy so Cloud Code can run it later.

    public enum StatKind { Wins, Kills, EffectiveHits, BossKills, Merges, Sends, CampaignStars, DailyBest, EndlessBest, RaidWins, TournamentWins, Discovered }

    [Serializable]
    public class AchievementDef
    {
        public string id;
        public StatKind stat;
        public int[] tiers;      // targets
        public int[] gems;       // reward per tier
        public string title;     // title unlocked by the last tier ("" = none)
        public int emote = -1;   // emote unlocked by the last tier
    }

    [Serializable]
    public class AchievementState { public string id; public int claimed; }

    [Serializable]
    public class DeckPreset { public string[] units = new string[0]; }

    public partial class PlayerState
    {
        // Bestiary
        public List<string> seenUnits = new List<string>();
        public List<string> seenEnemies = new List<string>();
        public List<string> bestiaryClaimed = new List<string>();
        // Achievements, titles, emotes
        public List<AchievementState> achievements = new List<AchievementState>();
        public string title = "";
        public List<string> titles = new List<string>();
        public List<int> emotes = new List<int>();
        // Lifetime stats
        public int statWins, statKills, statEffectiveHits, statBossKills, statMerges, statSends, statRaidWins, statTournamentWins;
        // Daily challenge
        public int dailyDay = -1, dailyBest, dailyClaimedMask, dailyRecord;
        // Endless
        public int endlessWeek = -1, endlessBest, endlessClaimedMask, endlessRecord;
        // Campaign: stars per level (0 = not cleared)
        public List<int> campaignStars = new List<int>();
        // Mayhem (weekend)
        public int mayhemWeek = -1, mayhemWins;
        // Raid
        public int raidDay = -1, raidRewarded;
        // Tournament
        public int tournamentWeek = -1, tournamentRound, tournamentEntries;
        public bool tournamentOut;
        // Ranked season (4 weeks, same clock as the pass season)
        public int rankedSeason = -1, seasonBestTrophies, pendingSeasonTier;
    }

    /// <summary>What a non-ranked mode gave (shown on the mode result screen).</summary>
    public sealed class ModeRewards
    {
        public List<Reward> rewards = new List<Reward>();
        public bool newRecord;
        public int best;
        public int stars, previousStars;
        public int tournamentRound;   // round reached (0-based) after this match
        public bool tournamentWon, tournamentOut;
    }

    public static class MetaContent
    {
        public const int BaseEmotes = 6, ExtraEmotes = 6;
        public const int BestiaryGems = 5;

        public static readonly List<AchievementDef> Achievements = new List<AchievementDef>
        {
            new AchievementDef { id = "winner", stat = StatKind.Wins, tiers = new[] { 10, 50, 200 }, gems = new[] { 10, 20, 50 }, title = "champion" },
            new AchievementDef { id = "hunter", stat = StatKind.Kills, tiers = new[] { 500, 5000, 30000 }, gems = new[] { 10, 20, 50 }, title = "hunter", emote = 6 },
            new AchievementDef { id = "counter", stat = StatKind.EffectiveHits, tiers = new[] { 200, 2000, 15000 }, gems = new[] { 10, 20, 50 }, title = "element_master", emote = 7 },
            new AchievementDef { id = "boss", stat = StatKind.BossKills, tiers = new[] { 5, 40, 200 }, gems = new[] { 10, 25, 50 }, title = "boss_slayer", emote = 8 },
            new AchievementDef { id = "merger", stat = StatKind.Merges, tiers = new[] { 100, 1000, 5000 }, gems = new[] { 10, 20, 40 }, title = "breeder" },
            new AchievementDef { id = "sender", stat = StatKind.Sends, tiers = new[] { 50, 500, 2500 }, gems = new[] { 10, 20, 40 }, title = "trickster", emote = 9 },
            new AchievementDef { id = "explorer", stat = StatKind.CampaignStars, tiers = new[] { 15, 75, 150 }, gems = new[] { 15, 30, 60 }, title = "explorer", emote = 10 },
            new AchievementDef { id = "daily", stat = StatKind.DailyBest, tiers = new[] { 10, 20, 30 }, gems = new[] { 10, 20, 40 }, title = "daily_star" },
            new AchievementDef { id = "endless", stat = StatKind.EndlessBest, tiers = new[] { 15, 30, 45 }, gems = new[] { 10, 25, 50 }, title = "unbreakable" },
            new AchievementDef { id = "raider", stat = StatKind.RaidWins, tiers = new[] { 1, 10, 50 }, gems = new[] { 10, 25, 50 }, title = "raider", emote = 11 },
            new AchievementDef { id = "cup", stat = StatKind.TournamentWins, tiers = new[] { 1, 5, 20 }, gems = new[] { 20, 40, 80 }, title = "cup_winner" },
            new AchievementDef { id = "zoologist", stat = StatKind.Discovered, tiers = new[] { 15, 30, 46 }, gems = new[] { 10, 20, 40 }, title = "zoologist" },
        };

        public static AchievementDef Achievement(string id)
        {
            foreach (var a in Achievements) if (a.id == id) return a;
            return null;
        }
    }

    public sealed partial class MetaService
    {
        // ------------------------------------------------------------------ helpers

        void NormalizeModes()
        {
            var s = State;
            if (s.seenUnits == null) s.seenUnits = new List<string>();
            if (s.seenEnemies == null) s.seenEnemies = new List<string>();
            if (s.bestiaryClaimed == null) s.bestiaryClaimed = new List<string>();
            if (s.achievements == null) s.achievements = new List<AchievementState>();
            if (s.titles == null) s.titles = new List<string>();
            if (s.emotes == null) s.emotes = new List<int>();
            if (s.campaignStars == null) s.campaignStars = new List<int>();
            if (s.title == null) s.title = "";
            while (s.campaignStars.Count < Campaign.Count) s.campaignStars.Add(0);
        }

        void RefreshModes(long now)
        {
            NormalizeModes();
            var s = State;
            int day = GameCalendar.DayIndex(now), week = GameCalendar.WeekIndex(now);
            if (s.dailyDay != day) { s.dailyDay = day; s.dailyBest = 0; s.dailyClaimedMask = 0; }
            if (s.endlessWeek != week) { s.endlessWeek = week; s.endlessBest = 0; s.endlessClaimedMask = 0; }
            if (s.mayhemWeek != week) { s.mayhemWeek = week; s.mayhemWins = 0; }
            if (s.raidDay != day) { s.raidDay = day; s.raidRewarded = 0; }
            if (s.tournamentWeek != week) { s.tournamentWeek = week; s.tournamentRound = 0; s.tournamentOut = false; s.tournamentEntries = 0; }

            // Ranked season: same 28-day clock as the pass. At the change: reward by best arena + soft trophy reset.
            int season = Season(now);
            if (s.rankedSeason != season)
            {
                if (s.rankedSeason >= 0 && s.seasonBestTrophies > 0) s.pendingSeasonTier = ArenaOf(s.seasonBestTrophies);
                if (s.rankedSeason >= 0 && s.trophies > SeasonResetFloor) s.trophies = SeasonResetFloor + (s.trophies - SeasonResetFloor) / 2;
                s.rankedSeason = season;
                s.seasonBestTrophies = s.trophies;
            }
        }

        public const int SeasonResetFloor = 1200;

        int ArenaOf(int trophies)
        {
            int a = 1;
            for (int i = 0; i < Config.arenaTrophies.Length; i++) if (trophies >= Config.arenaTrophies[i]) a = i + 1;
            return a;
        }

        public int Stat(StatKind k)
        {
            var s = State;
            switch (k)
            {
                case StatKind.Wins: return s.statWins;
                case StatKind.Kills: return s.statKills;
                case StatKind.EffectiveHits: return s.statEffectiveHits;
                case StatKind.BossKills: return s.statBossKills;
                case StatKind.Merges: return s.statMerges;
                case StatKind.Sends: return s.statSends;
                case StatKind.CampaignStars: return TotalStars();
                case StatKind.DailyBest: return s.dailyRecord;
                case StatKind.EndlessBest: return s.endlessRecord;
                case StatKind.RaidWins: return s.statRaidWins;
                case StatKind.TournamentWins: return s.statTournamentWins;
                case StatKind.Discovered: return s.seenUnits.Count + s.seenEnemies.Count;
            }
            return 0;
        }

        public int TotalStars()
        {
            int n = 0;
            foreach (var x in State.campaignStars) n += x;
            return n;
        }

        // ------------------------------------------------------------------ bestiary

        /// <summary>Marks animals / monsters as met. Returns the ids that are new.</summary>
        public List<string> Discover(IEnumerable<string> units, IEnumerable<string> enemies)
        {
            NormalizeModes();
            var fresh = new List<string>();
            if (units != null) foreach (var id in units) if (Content.UnitIndex(id) >= 0 && !State.seenUnits.Contains(id)) { State.seenUnits.Add(id); fresh.Add(id); }
            if (enemies != null) foreach (var id in enemies) if (Content.EnemyIndex(id) >= 0 && !State.seenEnemies.Contains(id)) { State.seenEnemies.Add(id); fresh.Add(id); }
            return fresh;
        }

        public bool IsSeen(string id) { NormalizeModes(); return State.seenUnits.Contains(id) || State.seenEnemies.Contains(id); }
        public bool CanClaimBestiary(string id) { return IsSeen(id) && !State.bestiaryClaimed.Contains(id); }

        public int ClaimableBestiary()
        {
            NormalizeModes();
            int n = 0;
            foreach (var id in State.seenUnits) if (!State.bestiaryClaimed.Contains(id)) n++;
            foreach (var id in State.seenEnemies) if (!State.bestiaryClaimed.Contains(id)) n++;
            return n;
        }

        /// <summary>First look at a new page of the bestiary: a few gems.</summary>
        public List<Reward> ClaimBestiary(string id)
        {
            if (!CanClaimBestiary(id)) return null;
            State.bestiaryClaimed.Add(id);
            var log = new List<Reward>();
            Grant(new Reward(RewardType.Gems, MetaContent.BestiaryGems), log);
            return log;
        }

        // ------------------------------------------------------------------ achievements, titles, emotes

        public int AchievementClaimed(string id)
        {
            NormalizeModes();
            foreach (var a in State.achievements) if (a.id == id) return a.claimed;
            return 0;
        }

        public bool CanClaimAchievement(AchievementDef def)
        {
            int c = AchievementClaimed(def.id);
            return c < def.tiers.Length && Stat(def.stat) >= def.tiers[c];
        }

        public int ClaimableAchievements()
        {
            int n = 0;
            foreach (var d in MetaContent.Achievements) if (CanClaimAchievement(d)) n++;
            return n;
        }

        public List<Reward> ClaimAchievement(string id)
        {
            var def = MetaContent.Achievement(id);
            if (def == null || !CanClaimAchievement(def)) return null;
            AchievementState st = null;
            foreach (var a in State.achievements) if (a.id == id) st = a;
            if (st == null) { st = new AchievementState { id = id }; State.achievements.Add(st); }
            var log = new List<Reward>();
            Grant(new Reward(RewardType.Gems, def.gems[st.claimed]), log);
            st.claimed++;
            if (st.claimed == def.tiers.Length)
            {
                if (!string.IsNullOrEmpty(def.title)) UnlockTitle(def.title);
                if (def.emote >= 0) UnlockEmote(def.emote);
            }
            return log;
        }

        public void UnlockTitle(string title) { NormalizeModes(); if (!State.titles.Contains(title)) State.titles.Add(title); }
        public bool SetTitle(string title)
        {
            NormalizeModes();
            if (!string.IsNullOrEmpty(title) && !State.titles.Contains(title)) return false;
            State.title = title ?? "";
            return true;
        }

        public void UnlockEmote(int emote) { NormalizeModes(); if (emote >= MetaContent.BaseEmotes && !State.emotes.Contains(emote)) State.emotes.Add(emote); }
        public bool HasEmote(int emote) { NormalizeModes(); return emote < MetaContent.BaseEmotes || State.emotes.Contains(emote); }

        // ------------------------------------------------------------------ seasons

        /// <summary>End-of-season reward for the best arena reached (1-5), then cleared.</summary>
        public List<Reward> ClaimSeasonReward()
        {
            int tier = State.pendingSeasonTier;
            if (tier <= 0) return null;
            State.pendingSeasonTier = 0;
            var log = new List<Reward>();
            Grant(new Reward(RewardType.Gold, 200 * tier), log);
            Grant(new Reward(RewardType.Gems, 20 * tier), log);
            Grant(new Reward(RewardType.Chest, 1, tier >= 4 ? "magic" : (tier >= 2 ? "gold" : "silver")), log);
            if (tier >= 3) UnlockEmote(MetaContent.BaseEmotes + 5);
            UnlockTitle("season_" + tier);
            return Merge(log);
        }

        // ------------------------------------------------------------------ mode results

        void CountStats(MatchSummary m)
        {
            var s = State;
            if (m.winner == 0) s.statWins++;
            s.statKills += m.kills;
            s.statEffectiveHits += m.effectiveHits;
            s.statBossKills += m.bossKills;
            s.statMerges += m.merges;
            s.statSends += m.sends;
            Progress(MissionType.EffectiveHits, m.effectiveHits);
            Progress(MissionType.KillBosses, m.bossKills);
            Discover(m.unitsUsed, m.enemiesSeen);
        }

        /// <summary>Records and rewards of a non-ranked mode match (Daily, Endless, Campaign, Mayhem, Raid, Tournament).</summary>
        public ModeRewards ApplyMode(MatchSummary m, long now)
        {
            Refresh(now);
            var s = State;
            var r = new ModeRewards();
            bool won = m.winner == 0;
            CountStats(m);
            Progress(MissionType.PlayMatches, 1);
            Progress(MissionType.PlayModes, 1);
            Progress(MissionType.KillEnemies, m.kills);
            Progress(MissionType.MergeUnits, m.merges);
            ProgressMax(MissionType.ReachWave, m.wave);
            if (won) Progress(MissionType.WinMatches, 1);
            var log = r.rewards;
            switch (m.mode)
            {
                case MatchMode.Daily:
                {
                    Progress(MissionType.DailyChallenge, 1);
                    r.newRecord = m.wave > s.dailyBest;
                    s.dailyBest = Math.Max(s.dailyBest, m.wave);
                    s.dailyRecord = Math.Max(s.dailyRecord, m.wave);
                    r.best = s.dailyBest;
                    for (int i = 0; i < DailyChallenge.MilestoneWaves.Length; i++)
                    {
                        if (m.wave < DailyChallenge.MilestoneWaves[i] || (s.dailyClaimedMask & (1 << i)) != 0) continue;
                        s.dailyClaimedMask |= 1 << i;
                        foreach (var x in DailyMilestone(i)) Grant(x, log);
                    }
                    Grant(new Reward(RewardType.PassXp, 20), log);
                    break;
                }
                case MatchMode.Endless:
                {
                    r.newRecord = m.wave > s.endlessBest;
                    s.endlessBest = Math.Max(s.endlessBest, m.wave);
                    s.endlessRecord = Math.Max(s.endlessRecord, m.wave);
                    r.best = s.endlessBest;
                    for (int i = 0; i < EndlessMilestones.Length; i++)
                    {
                        if (m.wave < EndlessMilestones[i] || (s.endlessClaimedMask & (1 << i)) != 0) continue;
                        s.endlessClaimedMask |= 1 << i;
                        Grant(new Reward(RewardType.Gems, 10 + 10 * i), log);
                    }
                    Grant(new Reward(RewardType.Gold, Math.Min(200, 4 * m.wave)), log);
                    Grant(new Reward(RewardType.PassXp, 20), log);
                    break;
                }
                case MatchMode.Campaign:
                {
                    var lv = Campaign.Get(m.level);
                    int stars = Campaign.Stars(lv, won, m.heartsLeft);
                    r.previousStars = s.campaignStars[lv.index];
                    r.stars = stars;
                    if (stars > 0 && r.previousStars == 0)
                    {
                        Grant(new Reward(RewardType.Gold, lv.gold), log);
                        if (lv.gems > 0) Grant(new Reward(RewardType.Gems, lv.gems), log);
                        if (lv.number == Campaign.LevelsPerRegion) Grant(new Reward(RewardType.Chest, 1, "gold"), log);
                    }
                    if (stars > r.previousStars) { Grant(new Reward(RewardType.Gems, 3 * (stars - r.previousStars)), log); s.campaignStars[lv.index] = stars; }
                    Grant(new Reward(RewardType.PassXp, won ? 30 : 10), log);
                    break;
                }
                case MatchMode.Mayhem:
                    if (won)
                    {
                        s.mayhemWins++;
                        Grant(new Reward(RewardType.Gold, 60), log);
                        if (s.mayhemWins == 3) Grant(new Reward(RewardType.Chest, 1, "gold"), log);
                    }
                    else Grant(new Reward(RewardType.Gold, 15), log);
                    Grant(new Reward(RewardType.PassXp, won ? 40 : 20), log);
                    break;
                case MatchMode.Raid:
                    if (won)
                    {
                        s.statRaidWins++;
                        Grant(new Reward(RewardType.Gold, 150), log);
                        if (s.raidRewarded < RaidChestsPerDay) { s.raidRewarded++; Grant(new Reward(RewardType.Chest, 1, "gold"), log); }
                    }
                    else Grant(new Reward(RewardType.Gold, 30), log);
                    Grant(new Reward(RewardType.PassXp, won ? 50 : 20), log);
                    break;
                case MatchMode.Tournament:
                    if (won)
                    {
                        s.tournamentRound++;
                        Grant(new Reward(RewardType.Gold, 80 * s.tournamentRound), log);
                        if (s.tournamentRound >= TournamentRules.Rounds)
                        {
                            r.tournamentWon = true;
                            s.statTournamentWins++;
                            Grant(new Reward(RewardType.Chest, 1, "magic"), log);
                            Grant(new Reward(RewardType.Gems, 50), log);
                            s.tournamentOut = true;   // run over (won): enter again next week or for gems
                        }
                    }
                    else { s.tournamentOut = true; Grant(new Reward(RewardType.Gold, 20 * (s.tournamentRound + 1)), log); }
                    r.tournamentRound = s.tournamentRound;
                    r.tournamentOut = s.tournamentOut;
                    Grant(new Reward(RewardType.PassXp, won ? 40 : 20), log);
                    break;
            }
            r.rewards = Merge(log);
            return r;
        }

        public static readonly int[] EndlessMilestones = { 10, 20, 30, 40 };
        public const int RaidChestsPerDay = 2;
        public const int TournamentReentryGems = 30;

        public static List<Reward> DailyMilestone(int i)
        {
            switch (i)
            {
                case 0: return new List<Reward> { new Reward(RewardType.Gold, 60) };
                case 1: return new List<Reward> { new Reward(RewardType.Gold, 120), new Reward(RewardType.Gems, 10) };
                case 2: return new List<Reward> { new Reward(RewardType.Chest, 1, "silver") };
                default: return new List<Reward> { new Reward(RewardType.Gems, 25) };
            }
        }

        /// <summary>Tournament entry: free once a week, then for gems. Starts a new 3-round run.</summary>
        public bool EnterTournament()
        {
            var s = State;
            bool free = s.tournamentEntries == 0;
            if (!free && s.gems < TournamentReentryGems) return false;
            if (!free) s.gems -= TournamentReentryGems;
            s.tournamentEntries++;
            s.tournamentRound = 0;
            s.tournamentOut = false;
            return true;
        }

        public bool TournamentRunActive { get { return State.tournamentEntries > 0 && !State.tournamentOut; } }

        /// <summary>Campaign: a level is open when the previous one has at least one star.</summary>
        public bool IsCampaignOpen(int index)
        {
            NormalizeModes();
            return index <= 0 || (index < Campaign.Count && State.campaignStars[index - 1] > 0);
        }
    }
}
