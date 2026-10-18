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

    public enum Rarity { Common = 0, Rare = 1, Epic = 2, Legendary = 3 }

    public enum EnemyKind { Normal = 0, Elite = 1, Boss = 2, Sent = 3 }

    public enum TargetMode { First = 0, Densest = 1, Strongest = 2 }

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
    }

    public enum BossSkill { None = 0, Summon = 1, StunUnit = 2, Heal = 3 }

    /// <summary>Keeper spells: the player casts them from the spell bar, paid with candy, then on cooldown.</summary>
    public enum SpellKind
    {
        Freeze = 1,     // stun enemies in radius for value ticks (bosses half)
        Meteor = 2,     // value damage (scaled by wave HP) in radius
        Gust = 3,       // push enemies in radius back value milli-tiles
        Rage = 4,       // all units attack value2 % faster for value ticks (no target)
        CandyRain = 5,  // kills give value2 % more candy for value ticks (no target)
        Lightning = 6,  // value damage (scaled by wave HP) to the strongest enemy (no target)
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

        public int firstWaveDelayTicks = 100;
        public int waveDurationTicks = 400;
        public int spawnWindowTicks = 300;
        public int baseEnemiesPerWave = 6;
        public int enemiesPerWaveStep = 1;
        public int maxEnemiesPerWave = 22;
        public int hpGrowthPercentPerWave = 10;
        /// <summary>Enemy HP scale of wave 1 (permille). Higher start + gentler growth = pressure from the first minute.</summary>
        public int waveHpStartPermille = 2000;
        public int eliteEveryNWaves = 3;
        public int bossEveryNWaves = 5;
        public int suddenDeathWave = 13;
        public int suddenDeathHpPercent = 15;
        public int waveBonusBase = 40;
        public int waveBonusPerWave = 15;
        public int killCandyGrowthPercentPerWave = 8;

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
        /// <summary>Extra candy for choosing which animal to summon (by rarity: common, rare, epic, legendary).
        /// The random Summon keeps the base price, so choosing is a real trade-off.</summary>
        public int[] summonPickExtra = { 10, 15, 25, 35 };

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

        // ---- Active unit skills (tap a glowing animal).
        public int skillMinLevel = 3;
        public int skillChargeTicks = 360;
        public int skillChargeReductionPerLevel = 30;

        public string[] normalEnemyIds = { "slime", "imp", "rock_beetle" };
        public string[] eliteEnemyIds = { "armored_knight" };
        public string[] bossEnemyIds = { "boss_summoner", "boss_stunner", "boss_healer" };
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
    }
}
