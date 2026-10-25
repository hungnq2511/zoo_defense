using System;
using System.Collections.Generic;

namespace ZooDefense.Core
{
    [Serializable]
    public class CardState
    {
        public string id;
        public int level;    // 0 = locked
        public int shards;   // spare shards toward the next level
        /// <summary>Talent picks (Talents.Pack: tier1 + 3 * tier2, 0 = none). Free to change outside a match.</summary>
        public int talent;
    }

    [Serializable]
    public class ChestSlot
    {
        public string chestId = "";   // "" = empty slot
        public long unlockEndsAt;     // 0 = not started (unix seconds)
    }

    [Serializable]
    public class MissionState
    {
        public string id;
        public int progress;
        public bool claimed;
    }

    /// <summary>Everything the economy owns for one player. Serialized as JSON (local now, Cloud Save later).</summary>
    [Serializable]
    public partial class PlayerState
    {
        public int version = 1;
        public int gold;
        public int gems;
        public int trophies;
        public List<CardState> cards = new List<CardState>();

        public ChestSlot[] chests = new ChestSlot[0];
        public int chestCycle;

        public int missionsDay = -1;
        public List<MissionState> missions = new List<MissionState>();

        public int passSeason = -1;
        public int passXp;
        public bool passPremium;
        public List<int> passClaimedFree = new List<int>();
        public List<int> passClaimedPremium = new List<int>();

        public int shopDay = -1;
        public List<string> dealUnits = new List<string>();
        public List<bool> dealBought = new List<bool>();
        public int freeGoldDay = -1;
        public int adGemsDay = -1;

        public int gachaPity;
        /// <summary>Pulls since the last SS (or better) / SSS.</summary>
        public int gachaPitySS, gachaPitySSS;
        /// <summary>Ranked wins in a row (bonus gold from the second one).</summary>
        public int winStreak;
        public uint rng;
    }

    /// <summary>Per-match numbers the economy needs (missions, rewards).</summary>
    public struct MatchSummary
    {
        public int winner;      // 0 = local player won, 1 = lost, -1 = draw
        public bool ranked;
        public int kills;
        public int merges;
        public int sends;
        public int wave;
        public int cardsBought;
        /// <summary>Rematch against the player who just beat you (bonus gold if you win).</summary>
        public bool revenge;
        /// <summary>Played against a real player online (not a bot).</summary>
        public bool online;
        /// <summary>Which board was ours (0 = host / offline, 1 = online guest).</summary>
        public int localPlayer;
        /// <summary>Game mode (MatchMode). Versus = ranked / practice.</summary>
        public MatchMode mode;
        /// <summary>Hits that countered the monster's element (missions / achievements).</summary>
        public int effectiveHits;
        public int bossKills;
        public int heartsLeft;
        /// <summary>Campaign level index (Campaign mode).</summary>
        public int level;
        /// <summary>Monster and animal ids met in the match (bestiary).</summary>
        public List<string> enemiesSeen;
        public List<string> unitsUsed;
        /// <summary>Opponent's hidden rating (online hello, or the bot's rating); 0 = unknown (rating unchanged).</summary>
        public int opponentMmr;
    }

    public struct MatchRewards
    {
        public int trophies;
        public int gold;
        public int passXp;
        public string chestId;      // chest placed in a slot, or null
        public bool chestSlotsFull; // a chest was earned but there was no room
        public int winStreak;       // ranked win streak after this match
        public int bonusGold;       // part of `gold` that came from the streak / revenge bonus
        public int mmrDelta;        // hidden rating change (not shown to the player)
    }

    public enum ChestStatus { Empty, Locked, Unlocking, Ready }
}
