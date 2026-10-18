namespace ZooDefense.Core
{
    /// <summary>
    /// Launch roster from the design doc: 20 animals (8 Common, 6 Rare, 4 Epic, 2 Legendary),
    /// wave enemies, bosses and 6 send-monsters. Numbers are starting values for playtesting;
    /// export them to JSON from the editor menu (Zoo Defense > Export Balance JSON) to tune
    /// without touching code.
    /// </summary>
    public static class DefaultContent
    {
        public static ContentDatabase CreateDatabase() { return new ContentDatabase(Create()); }

        public static ContentData Create()
        {
            var d = new ContentData();

            // ---------- Fire ----------
            d.units.Add(Unit("fire_fox", "Fire Fox", Element.Fire, Role.Damage, Rarity.Common, 30, 16, skill: UnitSkill.Burn, v1: 14));
            d.units.Add(Unit("firework_rooster", "Firework Rooster", Element.Fire, Role.Area, Rarity.Common, 22, 24, splash: 900));
            d.units.Add(Unit("ember_squirrel", "Ember Squirrel", Element.Fire, Role.Damage, Rarity.Rare, 18, 8));
            d.units.Add(Unit("baby_dragon", "Baby Dragon", Element.Fire, Role.Area, Rarity.Epic, 40, 22, splash: 1300));
            d.units.Add(Unit("phoenix_chick", "Phoenix Chick", Element.Fire, Role.Support, Rarity.Legendary, 26, 20, skill: UnitSkill.PhoenixBlessing, v1: 3));

            // ---------- Water ----------
            d.units.Add(Unit("bubble_frog", "Bubble Frog", Element.Water, Role.Control, Rarity.Common, 12, 20, slow: 40, slowTicks: 40, invis: true));
            d.units.Add(Unit("ice_penguin", "Ice Penguin", Element.Water, Role.Control, Rarity.Common, 15, 40, stun: 15, invis: true));
            d.units.Add(Unit("otter_cannon", "Otter Cannon", Element.Water, Role.Area, Rarity.Rare, 28, 22, splash: 1000));
            d.units.Add(Unit("ink_octopus", "Ink Octopus", Element.Water, Role.Control, Rarity.Rare, 14, 24, splash: 800, slow: 25, slowTicks: 40, invis: true, skill: UnitSkill.Reveal));
            d.units.Add(Unit("wave_dolphin", "Wave Dolphin", Element.Water, Role.Area, Rarity.Epic, 30, 40, splash: 1200, skill: UnitSkill.Knockback, v1: 450));

            // ---------- Grass ----------
            d.units.Add(Unit("spike_hedgehog", "Spike Hedgehog", Element.Grass, Role.Area, Rarity.Common, 20, 22, splash: 800));
            d.units.Add(Unit("carrot_bunny", "Carrot Bunny", Element.Grass, Role.Damage, Rarity.Common, 26, 14));
            d.units.Add(Unit("vine_sloth", "Vine Sloth", Element.Grass, Role.Control, Rarity.Common, 10, 30, splash: 1000, slow: 30, slowTicks: 40, invis: true));
            d.units.Add(Unit("chubby_panda", "Chubby Panda", Element.Grass, Role.Support, Rarity.Rare, 0, 20, skill: UnitSkill.CandyPerWave, v1: 5));
            d.units.Add(Unit("leaf_gecko", "Leaf Gecko", Element.Grass, Role.Control, Rarity.Rare, 16, 50, invis: true, skill: UnitSkill.PullStun, v1: 500, v2: 12));

            // ---------- Light ----------
            d.units.Add(Unit("sun_sparrow", "Sun Sparrow", Element.Light, Role.Damage, Rarity.Common, 24, 14, invis: true, skill: UnitSkill.Reveal));
            d.units.Add(Unit("lucky_cat", "Lucky Cat", Element.Light, Role.Support, Rarity.Rare, 12, 20, skill: UnitSkill.LuckyCandy, v1: 10, v2: 3));
            d.units.Add(Unit("night_owl", "Night Owl", Element.Light, Role.Support, Rarity.Epic, 10, 20, skill: UnitSkill.AttackSpeedAura, v1: 20, v2: 5));
            d.units.Add(Unit("meteor_deer", "Meteor Deer", Element.Light, Role.Area, Rarity.Epic, 55, 40, splash: 1400, target: TargetMode.Densest));
            d.units.Add(Unit("rainbow_unicorn", "Rainbow Unicorn", Element.Light, Role.Damage, Rarity.Legendary, 48, 16, skill: UnitSkill.AlwaysCounter));

            // ---------- Wave enemies ----------
            d.enemies.Add(Enemy("slime", "Slime", EnemyKind.Normal, 120, 50, leak: 1, candy: 8, points: 1, size: 600));
            d.enemies.Add(Enemy("imp", "Imp", EnemyKind.Normal, 80, 80, leak: 1, candy: 8, points: 1, size: 500));
            d.enemies.Add(Enemy("rock_beetle", "Rock Beetle", EnemyKind.Normal, 220, 35, leak: 1, candy: 10, points: 1, size: 700));
            d.enemies.Add(Enemy("armored_knight", "Armored Knight", EnemyKind.Elite, 700, 40, leak: 3, candy: 30, points: 3, size: 800, armor: 30, kbResist: 50));

            var summoner = Enemy("boss_summoner", "Summoner King", EnemyKind.Boss, 3000, 28, leak: 5, candy: 60, points: 8, size: 1100, kbResist: 80);
            summoner.bossSkill = BossSkill.Summon; summoner.bossSkillIntervalTicks = 100; summoner.summonEnemyId = "imp";
            d.enemies.Add(summoner);
            var stunner = Enemy("boss_stunner", "Thunder Toad", EnemyKind.Boss, 3200, 28, leak: 5, candy: 60, points: 8, size: 1100, kbResist: 80);
            stunner.bossSkill = BossSkill.StunUnit; stunner.bossSkillIntervalTicks = 120;
            d.enemies.Add(stunner);
            var healer = Enemy("boss_healer", "Mushroom Witch", EnemyKind.Boss, 2800, 28, leak: 5, candy: 60, points: 8, size: 1100, kbResist: 80);
            healer.bossSkill = BossSkill.Heal; healer.bossSkillIntervalTicks = 100;
            d.enemies.Add(healer);

            // ---------- Send-monsters (spawned on the opponent's board) ----------
            d.enemies.Add(Enemy("send_rat", "Speedy Rat", EnemyKind.Sent, 60, 100, leak: 1, candy: 0, points: 0, size: 400));
            var ghost = Enemy("send_ghost", "Sneaky Ghost", EnemyKind.Sent, 400, 50, leak: 2, candy: 0, points: 0, size: 600);
            ghost.invisible = true;
            d.enemies.Add(ghost);
            d.enemies.Add(Enemy("send_turtle", "Shield Turtle", EnemyKind.Sent, 1100, 30, leak: 2, candy: 0, points: 0, size: 800, areaResist: 50, kbResist: 50));
            var crow = Enemy("send_crow", "Candy Crow", EnemyKind.Sent, 350, 70, leak: 1, candy: 0, points: 0, size: 600);
            crow.stealCandy = 30;
            d.enemies.Add(crow);
            var golem = Enemy("send_golem", "Brick Golem", EnemyKind.Sent, 1300, 32, leak: 2, candy: 0, points: 0, size: 900, kbResist: 60);
            golem.locksCellOnDeath = true;
            d.enemies.Add(golem);
            var king = Enemy("send_king", "Mini Monster King", EnemyKind.Sent, 3500, 28, leak: 5, candy: 0, points: 0, size: 1000, kbResist: 80);
            king.bossSkill = BossSkill.Summon; king.bossSkillIntervalTicks = 120; king.summonEnemyId = "send_rat";
            d.enemies.Add(king);

            // PvP 2.0: candy price + income per wave. Cheap sends invest (more income), big sends hit hard (less income).
            d.sends.Add(Send("rat_rush", "Rat Rush", "send_rat", 3, 35, income: 6, element: Element.Grass, spacing: 10));
            d.sends.Add(Send("sneaky_ghost", "Sneaky Ghost", "send_ghost", 1, 50, income: 5, element: Element.Water));
            d.sends.Add(Send("shield_turtle", "Shield Turtle", "send_turtle", 1, 60, income: 4, element: Element.Water));
            d.sends.Add(Send("candy_crow", "Candy Crow", "send_crow", 1, 65, income: 4, element: Element.Fire));
            d.sends.Add(Send("brick_golem", "Brick Golem", "send_golem", 1, 100, income: 2, element: Element.Grass));
            d.sends.Add(Send("mini_king", "Mini Monster King", "send_king", 1, 160, income: 0, element: Element.Fire, once: true));
            // Not in any deck: what the boss-duel winner throws at the loser.
            d.sends.Add(Send("boss_remnant", "Boss Remnant", "armored_knight", 2, 0, income: 0, element: Element.Fire, spacing: 25));

            d.spells.AddRange(CreateSpells());
            return d;
        }

        /// <summary>Keeper spells. Damage values scale with the wave's enemy HP so they stay useful late.</summary>
        public static SpellDef[] CreateSpells()
        {
            return new[]
            {
                new SpellDef { id = "freeze", displayName = "Freeze", kind = SpellKind.Freeze, cost = 35, cooldownTicks = 400, radius = 1400, value = 50 },
                new SpellDef { id = "meteor", displayName = "Meteor", kind = SpellKind.Meteor, cost = 45, cooldownTicks = 320, radius = 1100, value = 220 },
                new SpellDef { id = "gust", displayName = "Gust", kind = SpellKind.Gust, cost = 35, cooldownTicks = 420, radius = 1500, value = 1600 },
                new SpellDef { id = "rage", displayName = "Rage", kind = SpellKind.Rage, cost = 45, cooldownTicks = 500, radius = 0, value = 120, value2 = 60 },
                new SpellDef { id = "candy_rain", displayName = "Candy Rain", kind = SpellKind.CandyRain, cost = 25, cooldownTicks = 600, radius = 0, value = 300, value2 = 60 },
                new SpellDef { id = "lightning", displayName = "Lightning", kind = SpellKind.Lightning, cost = 40, cooldownTicks = 360, radius = 0, value = 700 },
            };
        }

        public static readonly string[] DefaultSpells = { "freeze", "meteor", "gust", "rage" };

        public static readonly string[] DefaultDeck = { "fire_fox", "bubble_frog", "spike_hedgehog", "otter_cannon", "chubby_panda" };
        public static readonly string[] DefaultSendDeck = { "rat_rush", "sneaky_ghost", "shield_turtle", "brick_golem" };

        static UnitDef Unit(string id, string name, Element e, Role role, Rarity rarity, int dmg, int interval,
            int splash = 0, int slow = 0, int slowTicks = 0, int stun = 0, bool invis = false,
            UnitSkill skill = UnitSkill.None, int v1 = 0, int v2 = 0, TargetMode target = TargetMode.First)
        {
            return new UnitDef
            {
                id = id, displayName = name, element = e, role = role, rarity = rarity,
                damage = dmg, attackIntervalTicks = interval, splashRadius = splash,
                slowPercent = slow, slowTicks = slowTicks, stunTicks = stun,
                canHitInvisible = invis || skill == UnitSkill.Reveal,
                targetMode = target, skill = skill, skillValue = v1, skillValue2 = v2,
            };
        }

        static EnemyDef Enemy(string id, string name, EnemyKind kind, int hp, int speed, int leak, int candy, int points,
            int size, int armor = 0, int areaResist = 0, int kbResist = 0)
        {
            return new EnemyDef
            {
                id = id, displayName = name, kind = kind, element = Element.None, hp = hp, speed = speed,
                leakHearts = leak, candyReward = candy, sendPoints = points, size = size,
                armorPercent = armor, areaResistPercent = areaResist, knockbackResistPercent = kbResist,
            };
        }

        static SendDef Send(string id, string name, string enemyId, int count, int cost, int income = 0, Element element = Element.Fire, int spacing = 15, bool once = false)
        {
            return new SendDef { id = id, displayName = name, enemyId = enemyId, count = count, cost = cost, spacingTicks = spacing, oncePerMatch = once, income = income, element = element };
        }
    }
}
