using System;

namespace ZooDefense.Core
{
    /// <summary>
    /// Card talents: at card level 9 (tier 1) and 12 (tier 2) the player picks one of two branches for that animal.
    /// Branches depend on the animal's role, so every card gets a meaningful choice. Picks can be changed freely
    /// outside a match (adapt the deck to the meta). The sim only honours a tier when the card level allows it.
    /// </summary>
    public enum TalentKind
    {
        None = 0,
        // Damage
        BossHunter = 1,    // +90% damage to elites and bosses
        Interceptor = 2,   // +50% damage to monsters the opponent sent
        Crit = 3,          // every 3rd attack deals double damage
        Counter = 4,       // effective (countering) hits deal +75% more
        // Area
        WideBlast = 5,     // splash radius +30%
        FocusCore = 6,     // +35% damage to the main target
        FrostSmoke = 7,    // hits slow by 12% for 1 s
        Shatter = 8,       // hits break 12% armor for 2 s
        // Control
        Lingering = 9,     // slows and stuns last 60% longer
        Sting = 10,        // +25% damage
        BossBane = 11,     // stuns are not halved on bosses, +20% damage to bosses
        Mark = 12,         // hit monsters take +7% damage from everything for 2 s
        // Support
        Generous = 13,     // +3 candy per level at each wave
        Steadfast = 14,    // this animal cannot be stunned or locked by bosses
        Cheer = 15,        // orthogonal neighbours deal +10% damage
        QuickCharge = 16,  // active skill charges 30% faster
    }

    public static class Talents
    {
        public const int Tiers = 2;
        public const int Options = 2;

        /// <summary>Talent of a role / tier (0-1) / option (1-2). Option 0 = none.</summary>
        public static TalentKind Of(Role role, int tier, int option)
        {
            if (option < 1 || option > Options || tier < 0 || tier >= Tiers) return TalentKind.None;
            return (TalentKind)((int)role * 4 + tier * 2 + option);
        }

        /// <summary>Packed choice per card: tier1 + 3 * tier2 (each 0 none, 1 or 2).</summary>
        public static int Pack(int tier1, int tier2) { return Clamp(tier1) + 3 * Clamp(tier2); }
        public static int Tier1(int packed) { return Clamp(packed % 3); }
        public static int Tier2(int packed) { return Clamp(packed / 3 % 3); }
        static int Clamp(int v) { return v < 0 || v > Options ? 0 : v; }

        /// <summary>Card level needed for a tier (0-based).</summary>
        public static int UnlockLevel(BalanceConfig b, int tier) { return tier == 0 ? b.talentTier1Level : b.talentTier2Level; }

        /// <summary>Keeps only the tiers the card level has unlocked.</summary>
        public static int Validate(BalanceConfig b, int packed, int cardLevel)
        {
            int t1 = cardLevel >= b.talentTier1Level ? Tier1(packed) : 0;
            int t2 = cardLevel >= b.talentTier2Level ? Tier2(packed) : 0;
            return Pack(t1, t2);
        }
    }
}
