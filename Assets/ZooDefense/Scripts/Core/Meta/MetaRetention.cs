using System;
using System.Collections.Generic;

namespace ZooDefense.Core
{
    // Retention features: the 7-day login calendar, the hidden skill rating (MMR) used for matchmaking and bot
    // difficulty, and invite (referral) rewards. Pure C#, like the rest of the economy.

    public partial class PlayerState
    {
        /// <summary>Login calendar: rewards claimed in the current 7-day cycle (0..7).</summary>
        public int loginStep;
        /// <summary>Day index (unix days) of the last claimed login reward, -1 = never.</summary>
        public int loginLastDay = -1;
        /// <summary>Completed 7-day cycles (analytics, later cycles may grow the rewards).</summary>
        public int loginCycles;

        /// <summary>Hidden skill rating (Elo). Trophies only decide the arena; pairing and bots use this.</summary>
        public int mmr;
        /// <summary>Ranked matches counted in the rating (new players move faster).</summary>
        public int mmrGames;

        /// <summary>This player entered a friend's invite code (only once per account).</summary>
        public bool inviteRedeemed;
        /// <summary>Invite rewards already received for friends who joined with our code.</summary>
        public int inviteRewardsClaimed;
    }

    public partial class MetaConfig
    {
        /// <summary>The 7 login rewards, one per day (day 7 = magic chest). Missing a day does not reset the cycle.</summary>
        public List<Reward> loginRewards = new List<Reward>
        {
            new Reward(RewardType.Gold, 200),
            new Reward(RewardType.Gems, 20),
            new Reward(RewardType.Chest, 1, "silver"),
            new Reward(RewardType.Gold, 500),
            new Reward(RewardType.Gems, 40),
            new Reward(RewardType.Chest, 1, "gold"),
            new Reward(RewardType.Chest, 1, "magic"),
        };

        /// <summary>Gems for the new player who joined with a friend's code, and for that friend.</summary>
        public int inviteGems = 100;
    }

    public enum LoginDayState { Claimed, Today, Later }

    public sealed partial class MetaService
    {
        void NormalizeRetention()
        {
            if (State.mmr <= 0) State.mmr = MatchmakingRules.StartMmr + Math.Min(400, State.trophies / 4);
            if (Config.loginRewards == null || Config.loginRewards.Count == 0) Config.loginRewards = new MetaConfig().loginRewards;
            int days = Config.loginRewards.Count;
            if (State.loginStep < 0 || State.loginStep > days) State.loginStep = 0;
        }

        // ------------------------------------------------------------------ login calendar

        public int LoginDays { get { return Config.loginRewards.Count; } }

        /// <summary>A login reward can be claimed today (one per calendar day, UTC).</summary>
        public bool CanClaimLogin(long now) { return State.loginLastDay != Day(now); }

        /// <summary>Index (0..6) of the reward that today's claim gives.</summary>
        public int LoginTodayIndex(long now)
        {
            int step = State.loginStep >= LoginDays ? 0 : State.loginStep;
            return CanClaimLogin(now) ? step : Math.Max(0, step - 1);
        }

        /// <summary>State of a day box in the calendar.</summary>
        public LoginDayState LoginDay(int index, long now)
        {
            int step = State.loginStep;
            bool canClaim = CanClaimLogin(now);
            if (canClaim && step >= LoginDays) step = 0;            // a new cycle starts with today's claim
            if (index < step) return LoginDayState.Claimed;
            if (index == step && canClaim) return LoginDayState.Today;
            return LoginDayState.Later;
        }

        /// <summary>Claims today's login reward (null if already claimed today). A missed day only slows the cycle down.</summary>
        public List<Reward> ClaimLogin(long now)
        {
            if (!CanClaimLogin(now)) return null;
            if (State.loginStep >= LoginDays) { State.loginStep = 0; State.loginCycles++; }
            var reward = Config.loginRewards[State.loginStep];
            var log = new List<Reward>();
            Grant(reward, log);
            State.loginStep++;
            State.loginLastDay = Day(now);
            return Merge(log);
        }

        // ------------------------------------------------------------------ hidden rating

        /// <summary>Updates the hidden rating after a ranked match. Returns the change.</summary>
        public int ApplyRating(int winner, int opponentMmr)
        {
            int delta = MatchmakingRules.EloDelta(State.mmr, opponentMmr, winner, State.mmrGames);
            State.mmr = Math.Max(MatchmakingRules.MinMmr, State.mmr + delta);
            State.mmrGames++;
            return delta;
        }

        // ------------------------------------------------------------------ invites

        /// <summary>The new player entered a friend's code and the server accepted it.</summary>
        public List<Reward> RedeemInvite()
        {
            if (State.inviteRedeemed) return null;
            State.inviteRedeemed = true;
            var log = new List<Reward>();
            Grant(new Reward(RewardType.Gems, Config.inviteGems), log);
            return log;
        }

        /// <summary>The server counts `totalJoined` friends who joined with our code: grant the ones not rewarded yet.</summary>
        public List<Reward> ClaimInviteRewards(int totalJoined)
        {
            int fresh = Math.Min(50, totalJoined) - State.inviteRewardsClaimed;
            if (fresh <= 0) return null;
            State.inviteRewardsClaimed += fresh;
            var log = new List<Reward>();
            Grant(new Reward(RewardType.Gems, Config.inviteGems * fresh), log);
            return log;
        }
    }

    /// <summary>
    /// Matchmaking by hidden rating: real opponents are searched in a rating window that widens while waiting,
    /// and bots play at a difficulty and card level that fits the rating, so new players do not meet experts.
    /// </summary>
    public static class MatchmakingRules
    {
        public const int StartMmr = 1000;
        public const int MinMmr = 100;
        /// <summary>Rating windows (± points) tried one after the other while searching; the last is open.</summary>
        public static readonly int[] Windows = { 150, 300, 600, 100000 };

        public static int Window(int attempt) { return Windows[Math.Max(0, Math.Min(Windows.Length - 1, attempt))]; }

        /// <summary>Elo change for the player: winner 0 = player won, 1 = lost, -1 = draw.</summary>
        public static int EloDelta(int mine, int theirs, int winner, int gamesPlayed)
        {
            double expected = 1.0 / (1.0 + Math.Pow(10.0, (theirs - mine) / 400.0));
            double score = winner == 0 ? 1.0 : (winner == 1 ? 0.0 : 0.5);
            int k = gamesPlayed < 20 ? 48 : (gamesPlayed < 60 ? 32 : 24);
            return (int)Math.Round(k * (score - expected));
        }

        /// <summary>Bot strength 0-2 for a ranked match at this rating.</summary>
        public static int BotDifficulty(int mmr)
        {
            if (mmr < 950) return 0;
            if (mmr < 1250) return 1;
            return 2;
        }

        /// <summary>Card levels relative to the player's own: weaker bots for low ratings, stronger at the top.</summary>
        public static int BotLevelOffset(int mmr)
        {
            if (mmr < 850) return -1;
            if (mmr >= 1500) return 1;
            return 0;
        }

        /// <summary>The rating a bot opponent counts as (close to the player, so a win or loss moves the rating normally).</summary>
        public static int BotMmr(int mmr, int seed)
        {
            int spread = ((seed & 0x7fffffff) % 121) - 60;
            return Math.Max(MinMmr, mmr + spread);
        }
    }
}
