using System;
using System.Collections.Generic;

namespace ZooDefense.Core
{
    // All gameplay numbers are integers so the simulation is deterministic on every device
    // and on the server (replay validation). Units:
    //   distance  = milli-tiles (1 tile = 1000)
    //   time      = ticks (BalanceConfig.tickRate per second, default 20)
    //   percent   = whole percent (30 = 30%)

    public enum Element { None = 0, Fire = 1, Water = 2, Grass = 3, Light = 4 }

    public enum Role { Damage = 0, Area = 1, Control = 2, Support = 3 }

    /// <summary>
    /// Six tiers, C (most common) to SSS (rarest). The old names stay as aliases of the first four so older code and
    /// saved configs keep working. SS and SSS animals also give the whole board an aura (BalanceConfig.rarityAura*).
    /// </summary>
    public enum Rarity
    {
        C = 0, B = 1, A = 2, S = 3, SS = 4, SSS = 5,
        Common = C, Rare = B, Epic = A, Legendary = S,
    }

    public enum EnemyKind { Normal = 0, Elite = 1, Boss = 2, Sent = 3 }

    public enum TargetMode { First = 0, Densest = 1, Strongest = 2, Sent = 3 }   // Sent: monsters the opponent sent first

    public enum UnitSkill
    {
        None = 0,
        Burn = 1,             // level 4+: damage over time. value = burn damage per second
        Reveal = 2,           // can hit and reveals invisible enemies
        CandyPerWave = 3,     // value * level candy at each wave start
        AttackSpeedAura = 4,  // value + value2*(level-1) % attack speed to 4 neighbours
        LuckyCandy = 5,       // value + value2*(level-1) % chance of double candy on kill
        PhoenixBlessing = 6,  // every 3 waves: +1 level to a random Fire unit, unlock cells
        Knockback = 7,        // pushes hit enemies back by value milli-tiles
        PullStun = 8,         // pulls target back value milli-tiles and stuns value2 ticks
        AlwaysCounter = 9,    // always gets the element bonus
        // ---- Expansion 1. Level 4 and 7 unlock the upgrades described next to each value.
        BurnStack = 10,       // every hit adds a burn stack (max 3), value = burn per pulse; L4 longer, L7 every 5th hit spreads
        Pierce = 11,          // hits value targets in a line (70% after the first); L4 +1 target, L7 knockback 200 (not bosses)
        ArmorBreak = 12,      // -value % armor for 4 s (L4 value2); L7 every 4th hit explodes wider
        ChillSlow = 13,       // slow value % (L4 value2 %); L7 every 6th hit freezes 0.5 s (bosses: slow instead)
        Vulnerable = 14,      // +value % damage taken for 3 s (L4 value2); L7 every 5th hit spreads to 2 enemies
        Burst = 15,           // value shots per attack (L4 +1); L7 last shot +50% on slowed enemies
        NectarHaste = 16,     // support: +value % attack speed to the nearest attacker for 4 s (L4 value2, L7 2 allies)
        RootArea = 17,        // area root value ticks (L4 value2), 4 s immunity per enemy; bosses slowed instead
        Silence = 18,         // every 4th hit silences for value ticks (L4 value2): stops enemy abilities
        AegisGuard = 19,      // support: shields value allies (L4 +1) for 3 s: blocks stuns / cell locks, +value2 % damage
        // ---- Mythic (round 8, all SSS). Signature skills; level 4 and 7 upgrade them.
        StarFall = 20,        // every 6th attack (L7 4th): 2 stars (L4 3) on the densest groups, value % damage each
        TidalLance = 21,      // +value2 % on monsters above half HP; every 5th attack (L7 4th) a wave hits the whole lane behind the target at value %
        SolarFlare = 22,      // hits burn (value per pulse, stacks) and spread a stack to 1 neighbour (L7 2); +value2 % on burning monsters
        WorldTree = 23,       // value candy per level each wave; every 4 waves (L7 3) +1 level to an ally; L4 shields 2 allies (+value2 % damage)
        StarCharm = 24,       // slow value2 %; every 4th hit (L7 3rd) charms: the monster walks back for value ticks (bosses slowed). L4 +1 monster
        ChainLightning = 25,  // chains to value more monsters (L4 +1) at value2 %; L7 chained monsters are stunned briefly
        GlacierHowl = 26,     // slow value2 %; every 5th attack (L7 4th) freezes monsters around the target value ticks (L4 +4)
        MagmaCharge = 27,     // every 4th attack: wide blast +value % and knockback; L7 the blast burns (value2 per pulse)
        BloomDisplay = 28,    // support: value % attack speed (L4 +5) and value2 % damage to 4 nearest attackers (L7 6)
        CrystalRoll = 29,     // breaks value % armor (L4 +10); +value2 % on armored / elite / boss; L7 every 3rd roll is wider
        LanternGlow = 30,     // reveals monsters around the target, slow value %, mark value2 % (L4 +5); L7 every 5th hit stuns
        TigerPounce = 31,     // +value % on sent monsters; every 4th attack (L7 3rd) is a double-damage pounce; L4 pounce splashes a neighbour
        BambooCombo = 32,     // +value % per hit in a row on the same monster (max 5, L4 6, L7 8); L7 every 6th hit strikes twice
        CometShower = 33,     // every attack throws value extra comets (L4 +1, L7 +1) at other monsters, value2 % splash damage
        BubbleTrap = 34,      // slow value %; every 4th hit bubbles the target: stunned value2 ticks (L4 +4), +15 % damage taken. L7 bubble pops
    }

    /// <summary>Special behaviour of wave / elite monsters (bosses use BossSkill).</summary>
    public enum EnemyAbility
    {
        None = 0,
        DrumHaste = 1,   // every abilityInterval: +abilityValue % speed to up to 3 allies nearby for 2 s
        Enrage = 2,      // below 30% HP: +abilityValue % speed (once)
        Dash = 3,        // every abilityInterval: +abilityValue % speed for 0.7 s
        Burrow = 4,      // every abilityInterval: underground for abilityValue ticks (animals cannot hit it; spells still can)
        HealPulse = 5,   // every abilityInterval: heals monsters around it by abilityValue % of their max HP
        FogAura = 6,     // every abilityInterval: hides itself and the monsters around it again (until something reveals them)
        Fade = 7,        // every abilityInterval: turns invisible again (until something reveals it)
    }

    public enum BossSkill
    {
        None = 0, Summon = 1, StunUnit = 2, Heal = 3,
        /// <summary>Telegraphs a cell for 0.8 s, then locks that animal for 1.5 s. At 50% HP: +15% speed once.</summary>
        LockCell = 4,
        /// <summary>Telegraphs, then gains a shield of 8% max HP for 3 s. A broken shield makes it take +15% damage.</summary>
        VoidShield = 5,
    }

    /// <summary>Keeper spells: the player casts them from the spell bar, paid with candy, then on cooldown.</summary>
    public enum SpellKind
    {
        Freeze = 1,     // stun enemies in radius for value ticks (bosses half)
        Meteor = 2,     // value damage (scaled by wave HP) in radius
        Gust = 3,       // push enemies in radius back value milli-tiles
        Rage = 4,       // all units attack value2 % faster for value ticks (no target)
        CandyRain = 5,  // kills give value2 % more candy for value ticks (no target)
        Lightning = 6,  // value damage (scaled by wave HP) to the strongest enemy (no target)
        PoisonFog = 7,  // area: poison for 3 s (value per pulse, scaled by wave HP) and value2 % slow
        StarMark = 8,   // area: reveals and marks: +value % damage taken for value2 ticks
        ShieldDome = 9, // all your animals: block 1 stun / cell lock and +value2 % damage for value ticks (no target)
        LuckyStar = 10, // +1 level to your lowest-level animal (no target)
    }

    /// <summary>Choices offered every few waves; each player keeps the one they pick for the rest of the match.</summary>
    public enum PerkType
    {
        None = 0,
        FirePower = 1, WaterPower = 2, GrassPower = 3, LightPower = 4,   // +perkElementPercent damage for that element
        MergeCandy = 5,   // +perkMergeCandy candy per merge
        CheapSends = 6,   // sends cost 20% less
        CheapCards = 7,   // animal cards cost 15% less
        Interest = 8,     // at each wave start: +10% of current candy (max 60)
        ExtraHearts = 9,  // +2 hearts now
        SpellHaste = 10,  // spell cooldowns 30% shorter
        LuckyCrit = 11,   // 15% chance for any hit to deal double damage
        SendPoints = 12,  // +50% send points from kills
    }

    [Serializable]
    public class UnitDef
    {
        public string id;
        public string displayName;
        public Element element;
        public Role role;
        public Rarity rarity;
        public int damage;              // per hit at level 1
        public int attackIntervalTicks;
        public int splashRadius;        // milli-tiles, 0 = single target
        public int slowPercent;
        public int slowTicks;
        public int stunTicks;
        public bool canHitInvisible;
        public TargetMode targetMode;
        public UnitSkill skill;
        public int skillValue;
        public int skillValue2;
    }

    [Serializable]
    public class EnemyDef
    {
        public string id;
        public string displayName;
        public EnemyKind kind;
        public Element element;          // None = random Fire/Water/Grass at spawn
        public int hp;                   // at wave 1
        public int speed;                // milli-tiles per tick
        public int leakHearts;
        public int candyReward;
        public int sendPoints;
        public int armorPercent;         // reduces all damage
        public int areaResistPercent;    // reduces splash damage
        public int knockbackResistPercent;
        public bool invisible;           // only Control / Reveal units can target until hit
        public int stealCandy;           // on leak: steal from victim, give to sender
        public bool locksCellOnDeath;
        public BossSkill bossSkill;
        public int bossSkillIntervalTicks;
        public string summonEnemyId;
        public int size;                 // presentation hint, milli-tiles
        public EnemyAbility ability;
        public int abilityIntervalTicks;
        public int abilityValue;
        // Round 9 monsters
        public int splitCount;           // on death: splits into this many small copies (35% HP each, they do not split again)
        public int deathHealPercent;     // on death: heals monsters around it by this % of their max HP
        public int deathStunUnits;       // on death: stuns this many random animals for 1 s (shields block it)
    }

    [Serializable]
    public class SendDef
    {
        public string id;
        public string displayName;
        public string enemyId;
        public int count;
        /// <summary>Candy price (PvP 2.0: sends are paid with candy, like an investment).</summary>
        public int cost;
        public bool oncePerMatch;
        public int spacingTicks;
        /// <summary>Candy added to the sender's income every wave for the rest of the match.</summary>
        public int income;
        /// <summary>Element of the sent monsters (lets the defender counter them on purpose).</summary>
        public Element element;
    }

    [Serializable]
    public class SpellDef
    {
        public string id;
        public string displayName;
        public SpellKind kind;
        public int cost;            // candy
        public int cooldownTicks;
        public int radius;          // milli-tiles, 0 = no target needed
        public int value;
        public int value2;
        public bool NeedsTarget { get { return radius > 0; } }
    }

    [Serializable]
    public class BalanceConfig
    {
        /// <summary>Shallow copy (arrays are shared: replace them, never edit them in place).</summary>
        public BalanceConfig Clone() { return (BalanceConfig)MemberwiseClone(); }

        public int tickRate = 20;
        public int gridCols = 5;
        public int gridRows = 4;
        // Enemy path in milli-tiles. Grid cell (c, r) is centred at ((c+1)*1000, (r+1)*1000).
        // "Meadow loop": enemies come out of the cave at the top-left, walk along the top of the zoo, down the
        // right side and back along the bottom to the zoo gate at the bottom-left. Never crosses the grid.
        public int[] pathX = { 0, 6000, 6000, 0 };
        public int[] pathY = { 5200, 5200, -200, -200 };
        /// <summary>Global enemy speed multiplier, used to keep match length stable when the path changes.</summary>
        public int enemySpeedPercent = 66;

        public int startHearts = 12;
        public int startCandy = 100;
        public int shopSlots = 3;
        public int cardBasePrice = 20;
        public int cardPriceStep = 5;
        public int rerollCost = 15;
        public int maxLevel = 7;
        public int[] levelDamagePercent = { 100, 210, 330, 480, 680, 950, 1300 };
        public int elementBonusPercent = 30;
        /// <summary>Element triangle, the other way round: a Fire animal hitting a Water monster deals this % less.</summary>
        public int elementResistPercent = 25;
        /// <summary>Light is outside the triangle: never resisted, and this % extra against elites and bosses.</summary>
        public int lightVsBigPercent = 20;
        /// <summary>Chance (%) that a wave monster takes the element of the wave ("Fire wave"), shown in advance.</summary>
        public int waveElementPercent = 60;

        public int firstWaveDelayTicks = 100;
        public int waveDurationTicks = 400;
        public int spawnWindowTicks = 300;
        public int baseEnemiesPerWave = 6;
        public int enemiesPerWaveStep = 1;
        public int maxEnemiesPerWave = 22;
        public int hpGrowthPercentPerWave = 10;
        /// <summary>Enemy HP scale of wave 1 (permille). Higher start + gentler growth = pressure from the first minute.</summary>
        public int waveHpStartPermille = 1700;
        public int eliteEveryNWaves = 3;
        public int bossEveryNWaves = 5;
        public int suddenDeathWave = 12;
        public int suddenDeathHpPercent = 15;
        public int waveBonusBase = 40;
        public int waveBonusPerWave = 15;
        public int killCandyGrowthPercentPerWave = 8;

        /// <summary>Sends unlock when this wave starts: both players build a defence before the first attack (no opening rush).</summary>
        public int sendUnlockWave = 2;
        /// <summary>Max sends travelling to one player at the same time.</summary>
        public int maxIncomingSendsPerWave = 3;
        /// <summary>Sent monsters arrive this long after being sent (live, not at the next wave).</summary>
        public int sendTravelTicks = 100;
        /// <summary>HP of sent monsters relative to the current wave scaling (sends must threaten early).</summary>
        public int sendHpPercent = 400;
        /// <summary>Boss duel: the first player to kill the shared wave boss gets candy and sends this to the other.</summary>
        public int bossDuelCandy = 80;
        public string bossDuelSendId = "boss_remnant";
        public int sendWarningTicks = 60;
        public int comebackHeartGap = 3;
        public int comebackDiscountPercent = 20;
        public int lockWaves = 2;
        /// <summary>After being pushed or pulled back, a monster cannot be pushed again for this long (3 s).</summary>
        public int pushImmuneTicks = 60;
        /// <summary>Nectar Bee / Aegis Ram buff pulse (4 s).</summary>
        public int supportPulseTicks = 80;
        /// <summary>Send price and income scale (Mayhem "send storm").</summary>
        public int sendCostPercent = 100;
        public int sendIncomePercent = 100;
        public int unitStunTicks = 40;
        /// <summary>Extra damage per out-of-match card level above 1 (8 => level 15 deals 212%).</summary>
        public int cardLevelDamagePercent = 8;

        // ---- Positioning: "star" cells shared by both players (moved every few waves) and same-element links.
        public int starCellCount = 2;
        public int starCellBonusPercent = 30;
        public int starCellMoveEveryWaves = 5;
        public int elementLinkPercent = 8;            // per orthogonal neighbour of the same element

        // ---- In-match element upgrades (candy): +percent damage to every unit of that element per level.
        public int elementUpgradeMax = 5;
        public int elementUpgradeBaseCost = 60;
        public int elementUpgradeCostStep = 50;
        public int elementUpgradePercent = 10;
        /// <summary>Summon / per-animal upgrades: each deck animal can be powered up during the match.</summary>
        public int unitUpgradeMax = 5;
        public int unitUpgradeBaseCost = 50;
        public int unitUpgradeCostStep = 50;
        public int unitUpgradePercent = 15;
        /// <summary>Extra candy for choosing which animal to summon (by rarity C, B, A, S, SS, SSS).
        /// The random Summon keeps the base price, so choosing is a real trade-off.</summary>
        public int[] summonPickExtra = { 10, 15, 25, 35, 40, 50 };
        /// <summary>Rarity aura (by rarity C..SSS): while an animal of that tier stands on the board, EVERY animal of that
        /// board gets this % damage / attack speed. Only the best tier on the board counts (no stacking).</summary>
        public int[] rarityAuraDamagePercent = { 0, 0, 0, 0, 5, 8 };
        public int[] rarityAuraSpeedPercent = { 0, 0, 0, 0, 0, 8 };

        // ---- Candy balloons: float across the board, tap before they leave.
        public int balloonFirstDelayTicks = 300;
        public int balloonMinGapTicks = 260;
        public int balloonMaxGapTicks = 480;
        public int balloonLifeTicks = 110;
        public int balloonCandyBase = 15;
        public int balloonCandyPerWave = 4;

        // ---- Perk choice every N waves.
        public int perkEveryNWaves = 5;
        public int perkChoices = 3;
        public int perkAutoPickTicks = 300;
        public int perkElementPercent = 25;
        public int perkMergeCandy = 10;

        // ---- Reflect an incoming send back to the sender (paid with send points).
        public int reflectCostPercent = 60;

        // ---- Tactics: sell an animal, hex the opponent's next wave, card talents.
        /// <summary>Selling returns this % of the cards in the animal (base price x 2^(level-1)).</summary>
        public int sellRefundPercent = 40;
        /// <summary>Element Hex: forces every normal monster of the opponent's next wave into one element.</summary>
        public int hexUnlockWave = 3;
        public int hexBaseCost = 70;
        public int hexCostStep = 35;
        public int hexCostPercent = 100;
        /// <summary>PvP: monster HP follows both players' average card power (this % of it), so matches last about
        /// as long at every trophy level. Upgrades still matter against the opponent. 0 = off.</summary>
        public int waveHpCardScalePercent = 100;
        /// <summary>
        /// Evolve (tap an animal, pay candy, +1 level). Price = current card price x evolveCardCostPercent[level-1] %:
        /// 0.75 of a summon for level 1->2 rising to 1.25 for 6->7, so each step costs visibly more. Far cheaper than
        /// merging at the top (a level 7 by merges needs 64 summons), so level 7 stays reachable.
        /// Bot audit Oct 2026: evolving bot beats a non-evolving one ~55%; level 7 on ~6% of boards.
        /// </summary>
        public int[] evolveCardCostPercent = { 75, 85, 95, 105, 115, 125 };
        public int evolvePricePercent = 100;   // global multiplier on the table above
        /// <summary>Card level that unlocks talent tier 1 / tier 2.</summary>
        public int talentTier1Level = 9;
        public int talentTier2Level = 12;

        // ---- Active unit skills (tap a glowing animal).
        public int skillMinLevel = 3;
        public int skillChargeTicks = 360;
        public int skillChargeReductionPerLevel = 30;

        public string[] normalEnemyIds = { "slime", "imp", "rock_beetle", "bandit_raccoon", "crystal_bat",
            "spore_puff", "dash_lizard", "pebble_golem", "shadow_blob", "balloon_bat", "shield_beetle" };
        public string[] eliteEnemyIds = { "armored_knight", "drum_goblin", "armored_scorpion" };
        /// <summary>Boss rotation; each match starts at a different point (from the seed) so bosses vary.</summary>
        public string[] bossEnemyIds = { "boss_summoner", "clockwork_gorilla", "boss_stunner", "void_hermit_crab", "boss_healer" };
    }

    /// <summary>Serializable root used for the balance JSON (Resources/ZooBalance.json, later Remote Config).</summary>
    [Serializable]
    public class ContentData
    {
        public BalanceConfig balance = new BalanceConfig();
        public List<UnitDef> units = new List<UnitDef>();
        public List<EnemyDef> enemies = new List<EnemyDef>();
        public List<SendDef> sends = new List<SendDef>();
        public List<SpellDef> spells = new List<SpellDef>();
    }

    public static class ElementRules
    {
        /// <summary>Fire beats Grass, Grass beats Water, Water beats Fire.</summary>
        public static bool Counters(Element attacker, Element defender)
        {
            return (attacker == Element.Fire && defender == Element.Grass)
                || (attacker == Element.Grass && defender == Element.Water)
                || (attacker == Element.Water && defender == Element.Fire);
        }

        /// <summary>The element this one beats (None for Light / None).</summary>
        public static Element Beats(Element e)
        {
            switch (e) { case Element.Fire: return Element.Grass; case Element.Grass: return Element.Water; case Element.Water: return Element.Fire; default: return Element.None; }
        }

        /// <summary>The element that beats this one (None for Light / None).</summary>
        public static Element WeakTo(Element e)
        {
            switch (e) { case Element.Fire: return Element.Water; case Element.Grass: return Element.Fire; case Element.Water: return Element.Grass; default: return Element.None; }
        }

        /// <summary>-1 resisted, 0 neutral, 1 effective: how an attack of this element lands on a monster.</summary>
        public static int Matchup(Element attacker, Element defender, bool alwaysCounter = false)
        {
            if (alwaysCounter || Counters(attacker, defender)) return 1;
            if (Counters(defender, attacker)) return -1;
            return 0;
        }

        /// <summary>Damage % of a hit (100 = neutral). Light gets its bonus against elites and bosses instead.</summary>
        public static int DamagePercent(BalanceConfig b, Element attacker, Element defender, bool alwaysCounter, bool bigEnemy)
        {
            int m = Matchup(attacker, defender, alwaysCounter);
            int pct = m > 0 ? 100 + b.elementBonusPercent : (m < 0 ? 100 - b.elementResistPercent : 100);
            if (attacker == Element.Light && bigEnemy && m <= 0) pct += b.lightVsBigPercent;
            return pct;
        }
    }
}
