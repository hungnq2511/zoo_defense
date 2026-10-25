namespace ZooDefense.Core
{
    /// <summary>
    /// Roster: 61 animals in six rarity tiers (C 25, B 8, A 6, S 4, SS 2, SSS 16) plus
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
            d.units.Add(Unit("fire_fox", "Fire Fox", Element.Fire, Role.Damage, Rarity.B, 32, 16, skill: UnitSkill.Burn, v1: 14));
            d.units.Add(Unit("firework_rooster", "Firework Rooster", Element.Fire, Role.Area, Rarity.C, 22, 24, splash: 900));
            d.units.Add(Unit("ember_squirrel", "Ember Squirrel", Element.Fire, Role.Damage, Rarity.A, 18, 8));
            d.units.Add(Unit("baby_dragon", "Baby Dragon", Element.Fire, Role.Area, Rarity.S, 34, 22, splash: 1300));
            d.units.Add(Unit("phoenix_chick", "Phoenix Chick", Element.Fire, Role.Support, Rarity.SS, 26, 20, skill: UnitSkill.PhoenixBlessing, v1: 3));

            // ---------- Water ----------
            d.units.Add(Unit("bubble_frog", "Bubble Frog", Element.Water, Role.Control, Rarity.C, 12, 20, slow: 40, slowTicks: 40, invis: true));
            d.units.Add(Unit("ice_penguin", "Ice Penguin", Element.Water, Role.Control, Rarity.C, 20, 40, stun: 15, invis: true));
            d.units.Add(Unit("otter_cannon", "Otter Cannon", Element.Water, Role.Area, Rarity.A, 28, 22, splash: 1000));
            d.units.Add(Unit("ink_octopus", "Ink Octopus", Element.Water, Role.Control, Rarity.A, 14, 24, splash: 800, slow: 25, slowTicks: 40, invis: true, skill: UnitSkill.Reveal));
            d.units.Add(Unit("wave_dolphin", "Wave Dolphin", Element.Water, Role.Area, Rarity.A, 32, 40, splash: 1200, skill: UnitSkill.Knockback, v1: 450));

            // ---------- Grass ----------
            d.units.Add(Unit("spike_hedgehog", "Spike Hedgehog", Element.Grass, Role.Area, Rarity.C, 23, 22, splash: 800));
            d.units.Add(Unit("carrot_bunny", "Carrot Bunny", Element.Grass, Role.Damage, Rarity.C, 25, 14));
            d.units.Add(Unit("vine_sloth", "Vine Sloth", Element.Grass, Role.Control, Rarity.C, 10, 30, splash: 1000, slow: 30, slowTicks: 40, invis: true));
            d.units.Add(Unit("chubby_panda", "Chubby Panda", Element.Grass, Role.Support, Rarity.B, 0, 20, skill: UnitSkill.CandyPerWave, v1: 12));
            d.units.Add(Unit("leaf_gecko", "Leaf Gecko", Element.Grass, Role.Control, Rarity.B, 24, 44, invis: true, skill: UnitSkill.PullStun, v1: 500, v2: 18));

            // ---------- Light ----------
            d.units.Add(Unit("sun_sparrow", "Sun Sparrow", Element.Light, Role.Damage, Rarity.C, 22, 14, invis: true, skill: UnitSkill.Reveal));
            d.units.Add(Unit("lucky_cat", "Lucky Cat", Element.Light, Role.Support, Rarity.A, 12, 20, skill: UnitSkill.LuckyCandy, v1: 10, v2: 3));
            d.units.Add(Unit("night_owl", "Night Owl", Element.Light, Role.Support, Rarity.A, 10, 20, skill: UnitSkill.AttackSpeedAura, v1: 28, v2: 5));
            d.units.Add(Unit("meteor_deer", "Meteor Deer", Element.Light, Role.Area, Rarity.S, 55, 40, splash: 1400, target: TargetMode.Densest));
            d.units.Add(Unit("rainbow_unicorn", "Rainbow Unicorn", Element.Light, Role.Damage, Rarity.SSS, 40, 16, skill: UnitSkill.AlwaysCounter));

            // ---------- Expansion 1 (10 animals). Numbers converted to this game's damage / tick scale. ----------
            d.units.Add(Unit("lava_salamander", "Lava Salamander", Element.Fire, Role.Damage, Rarity.B, 29, 23, skill: UnitSkill.BurnStack, v1: 8));
            d.units.Add(Unit("ember_boar", "Ember Boar", Element.Fire, Role.Damage, Rarity.B, 43, 33, skill: UnitSkill.Pierce, v1: 2));
            d.units.Add(Unit("forge_mole", "Forge Mole", Element.Fire, Role.Area, Rarity.S, 55, 42, splash: 650, skill: UnitSkill.ArmorBreak, v1: 15, v2: 25));
            d.units.Add(Unit("snow_seal", "Snow Seal", Element.Water, Role.Control, Rarity.B, 27, 26, invis: true, skill: UnitSkill.ChillSlow, v1: 22, v2: 28));
            d.units.Add(Unit("coral_axolotl", "Coral Axolotl", Element.Water, Role.Control, Rarity.B, 26, 30, invis: true, skill: UnitSkill.Vulnerable, v1: 12, v2: 18));
            d.units.Add(Unit("snap_shrimp", "Snap Shrimp", Element.Water, Role.Damage, Rarity.C, 15, 28, skill: UnitSkill.Burst, v1: 3));
            d.units.Add(Unit("nectar_bee", "Nectar Bee", Element.Grass, Role.Support, Rarity.C, 0, 80, skill: UnitSkill.NectarHaste, v1: 25, v2: 30));
            d.units.Add(Unit("sprout_elephant", "Sprout Elephant", Element.Grass, Role.Control, Rarity.S, 45, 48, splash: 750, invis: true, skill: UnitSkill.RootArea, v1: 12, v2: 15));
            d.units.Add(Unit("moon_moth", "Moon Moth", Element.Light, Role.Control, Rarity.B, 41, 35, invis: true, skill: UnitSkill.Silence, v1: 20, v2: 25));
            d.units.Add(Unit("aegis_ram", "Aegis Ram", Element.Light, Role.Support, Rarity.SS, 30, 24, skill: UnitSkill.AegisGuard, v1: 4, v2: 27));

            // ---------- Mythic (round 8): 15 SSS animals with signature skills ----------
            d.units.Add(Unit("celestial_qilin", "Celestial Qilin", Element.Light, Role.Area, Rarity.SSS, 38, 26, splash: 1100, skill: UnitSkill.StarFall, v1: 100, target: TargetMode.Densest));
            d.units.Add(Unit("abyss_leviathan", "Abyss Leviathan", Element.Water, Role.Damage, Rarity.SSS, 38, 18, skill: UnitSkill.TidalLance, v1: 70, v2: 25));
            d.units.Add(Unit("solar_lion", "Solar Lion", Element.Fire, Role.Damage, Rarity.SSS, 36, 16, skill: UnitSkill.SolarFlare, v1: 10, v2: 30));
            d.units.Add(Unit("worldtree_tortoise", "World-Tree Tortoise", Element.Grass, Role.Support, Rarity.SSS, 0, 20, skill: UnitSkill.WorldTree, v1: 20, v2: 20));
            d.units.Add(Unit("ninetail_star_fox", "Nine-Tail Star Fox", Element.Light, Role.Control, Rarity.SSS, 24, 22, splash: 700, invis: true, skill: UnitSkill.StarCharm, v1: 14, v2: 15));
            d.units.Add(Unit("thunder_griffin", "Thunder Griffin", Element.Light, Role.Damage, Rarity.SSS, 34, 16, skill: UnitSkill.ChainLightning, v1: 2, v2: 60));
            d.units.Add(Unit("glacier_wolf", "Glacier Wolf", Element.Water, Role.Control, Rarity.SSS, 35, 24, invis: true, skill: UnitSkill.GlacierHowl, v1: 12, v2: 25));
            d.units.Add(Unit("magma_rhino", "Magma Rhino", Element.Fire, Role.Area, Rarity.SSS, 45, 30, splash: 900, skill: UnitSkill.MagmaCharge, v1: 80, v2: 10));
            d.units.Add(Unit("bloom_peacock", "Bloom Peacock", Element.Grass, Role.Support, Rarity.SSS, 18, 20, skill: UnitSkill.BloomDisplay, v1: 28, v2: 20));
            d.units.Add(Unit("crystal_pangolin", "Crystal Pangolin", Element.Light, Role.Area, Rarity.SSS, 36, 24, splash: 900, skill: UnitSkill.CrystalRoll, v1: 12, v2: 25));
            d.units.Add(Unit("lantern_jellyfish", "Lantern Jellyfish", Element.Water, Role.Control, Rarity.SSS, 22, 24, splash: 1000, invis: true, skill: UnitSkill.LanternGlow, v1: 20, v2: 8));
            d.units.Add(Unit("flame_tiger_cub", "Flame Tiger Cub", Element.Fire, Role.Damage, Rarity.SSS, 32, 14, skill: UnitSkill.TigerPounce, v1: 60));
            d.units.Add(Unit("bamboo_red_panda", "Bamboo Red Panda", Element.Grass, Role.Damage, Rarity.SSS, 18, 8, skill: UnitSkill.BambooCombo, v1: 12));
            d.units.Add(Unit("comet_hamster", "Comet Hamster", Element.Light, Role.Area, Rarity.SSS, 32, 22, splash: 800, skill: UnitSkill.CometShower, v1: 1, v2: 55));
            d.units.Add(Unit("puddle_duckling", "Puddle Duckling", Element.Water, Role.Control, Rarity.SSS, 26, 20, invis: true, skill: UnitSkill.BubbleTrap, v1: 30, v2: 16));

            // ---------- Wild friends (round 9): 16 common (C) animals, 4 per element, using the existing skills ----------
            d.units.Add(Unit("sprout_anteater", "Sprout Anteater", Element.Grass, Role.Damage, Rarity.C, 16, 24, skill: UnitSkill.Burst, v1: 2));
            d.units.Add(Unit("leaf_okapi", "Leaf Okapi", Element.Grass, Role.Damage, Rarity.C, 24, 14));
            d.units.Add(Unit("moss_kiwi", "Moss Kiwi", Element.Grass, Role.Control, Rarity.C, 21, 20, slow: 35, slowTicks: 40, invis: true));
            d.units.Add(Unit("bloom_snail", "Bloom Snail", Element.Grass, Role.Control, Rarity.C, 10, 30, splash: 800, slow: 40, slowTicks: 50));
            d.units.Add(Unit("drop_platypus", "Drop Platypus", Element.Water, Role.Control, Rarity.C, 20, 32, stun: 12, invis: true));
            d.units.Add(Unit("brook_moose", "Brook Moose", Element.Water, Role.Area, Rarity.C, 24, 32, splash: 900, skill: UnitSkill.Knockback, v1: 300));
            d.units.Add(Unit("frost_bear", "Frost Bear Cub", Element.Water, Role.Area, Rarity.C, 18, 26, splash: 800, skill: UnitSkill.ChillSlow, v1: 15, v2: 20));
            d.units.Add(Unit("river_otter", "River Otter", Element.Water, Role.Damage, Rarity.C, 17, 20, skill: UnitSkill.Burst, v1: 2));
            d.units.Add(Unit("moon_lemur", "Moon Lemur", Element.Light, Role.Support, Rarity.C, 10, 24, skill: UnitSkill.CandyPerWave, v1: 14));
            d.units.Add(Unit("star_eagle", "Star Eagle", Element.Light, Role.Damage, Rarity.C, 36, 20, skill: UnitSkill.Reveal));
            d.units.Add(Unit("sun_alpaca", "Sun Alpaca", Element.Light, Role.Area, Rarity.C, 20, 24, splash: 900));
            d.units.Add(Unit("prism_hummingbird", "Prism Hummingbird", Element.Light, Role.Damage, Rarity.C, 10, 6));
            d.units.Add(Unit("ember_devil", "Ember Devil", Element.Fire, Role.Damage, Rarity.C, 22, 20, skill: UnitSkill.BurnStack, v1: 5));
            d.units.Add(Unit("blaze_bison", "Blaze Bison", Element.Fire, Role.Area, Rarity.C, 24, 30, splash: 800, skill: UnitSkill.ArmorBreak, v1: 10, v2: 15));
            d.units.Add(Unit("torch_toucan", "Torch Toucan", Element.Fire, Role.Damage, Rarity.C, 24, 16, skill: UnitSkill.Burn, v1: 10));
            d.units.Add(Unit("chili_chameleon", "Chili Chameleon", Element.Fire, Role.Control, Rarity.C, 24, 18, invis: true, skill: UnitSkill.Silence, v1: 15, v2: 20));

            // ---------- Wave enemies ----------
            d.enemies.Add(Enemy("slime", "Slime", EnemyKind.Normal, 120, 50, leak: 1, candy: 8, points: 1, size: 600));
            d.enemies.Add(Enemy("imp", "Imp", EnemyKind.Normal, 80, 80, leak: 1, candy: 8, points: 1, size: 500));
            d.enemies.Add(Enemy("rock_beetle", "Rock Beetle", EnemyKind.Normal, 220, 35, leak: 1, candy: 10, points: 1, size: 700));
            d.enemies.Add(Enemy("armored_knight", "Armored Knight", EnemyKind.Elite, 700, 40, leak: 3, candy: 30, points: 3, size: 800, armor: 30, kbResist: 50));

            var raccoon = Enemy("bandit_raccoon", "Bandit Raccoon", EnemyKind.Normal, 140, 70, leak: 1, candy: 8, points: 1, size: 600);
            raccoon.stealCandy = 10;   // takes candy when it reaches the gate
            d.enemies.Add(raccoon);
            var bat = Enemy("crystal_bat", "Crystal Bat", EnemyKind.Normal, 100, 72, leak: 1, candy: 8, points: 1, size: 500);
            bat.ability = EnemyAbility.Dash; bat.abilityIntervalTicks = 100; bat.abilityValue = 30;
            d.enemies.Add(bat);
            var drum = Enemy("drum_goblin", "Drum Goblin", EnemyKind.Elite, 600, 40, leak: 3, candy: 30, points: 3, size: 800, armor: 10, kbResist: 40);
            drum.ability = EnemyAbility.DrumHaste; drum.abilityIntervalTicks = 100; drum.abilityValue = 12;
            d.enemies.Add(drum);
            var scorpion = Enemy("armored_scorpion", "Armored Scorpion", EnemyKind.Elite, 650, 38, leak: 3, candy: 30, points: 3, size: 800, armor: 40, kbResist: 50);
            scorpion.ability = EnemyAbility.Enrage; scorpion.abilityValue = 15;
            d.enemies.Add(scorpion);
            // Round 9 wave monsters
            var spore = Enemy("spore_puff", "Spore Puff", EnemyKind.Normal, 110, 45, leak: 1, candy: 9, points: 1, size: 650);
            spore.deathHealPercent = 10;   // bursts into healing spores
            d.enemies.Add(spore);
            var lizard = Enemy("dash_lizard", "Dash Lizard", EnemyKind.Normal, 80, 60, leak: 1, candy: 8, points: 1, size: 550);
            lizard.ability = EnemyAbility.Dash; lizard.abilityIntervalTicks = 60; lizard.abilityValue = 100;
            d.enemies.Add(lizard);
            d.enemies.Add(Enemy("pebble_golem", "Pebble Golem", EnemyKind.Normal, 180, 32, leak: 1, candy: 10, points: 1, size: 700, armor: 15, kbResist: 30));
            var blob = Enemy("shadow_blob", "Shadow Blob", EnemyKind.Normal, 95, 52, leak: 1, candy: 8, points: 1, size: 550);
            blob.ability = EnemyAbility.Fade; blob.abilityIntervalTicks = 100;
            d.enemies.Add(blob);
            d.enemies.Add(Enemy("balloon_bat", "Balloon Bat", EnemyKind.Normal, 100, 58, leak: 1, candy: 8, points: 1, size: 550, kbResist: 100));
            d.enemies.Add(Enemy("shield_beetle", "Shield Beetle", EnemyKind.Normal, 125, 40, leak: 1, candy: 9, points: 1, size: 650, areaResist: 50));

            var gorilla = Enemy("clockwork_gorilla", "Clockwork Gorilla", EnemyKind.Boss, 3300, 26, leak: 5, candy: 60, points: 8, size: 1100, armor: 25, kbResist: 80);
            gorilla.bossSkill = BossSkill.LockCell; gorilla.bossSkillIntervalTicks = 140;
            d.enemies.Add(gorilla);
            var crab = Enemy("void_hermit_crab", "Void Hermit Crab", EnemyKind.Boss, 3400, 24, leak: 5, candy: 60, points: 8, size: 1100, armor: 15, kbResist: 80);
            crab.bossSkill = BossSkill.VoidShield; crab.bossSkillIntervalTicks = 160;
            d.enemies.Add(crab);

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

            // Round 9 send-monsters: new ways to pressure, no gacha (all unlocked).
            var splitter = Enemy("send_splitter", "Twin Slime", EnemyKind.Sent, 700, 45, leak: 2, candy: 0, points: 0, size: 750);
            splitter.splitCount = 2;
            d.enemies.Add(splitter);
            var mole = Enemy("send_mole", "Digger Mole", EnemyKind.Sent, 600, 55, leak: 2, candy: 0, points: 0, size: 700, kbResist: 40);
            mole.ability = EnemyAbility.Burrow; mole.abilityIntervalTicks = 80; mole.abilityValue = 30;
            d.enemies.Add(mole);
            var shroom = Enemy("send_mushroom", "Healer Shroom", EnemyKind.Sent, 500, 45, leak: 1, candy: 0, points: 0, size: 650);
            shroom.ability = EnemyAbility.HealPulse; shroom.abilityIntervalTicks = 40; shroom.abilityValue = 5;
            d.enemies.Add(shroom);
            var wisp = Enemy("send_wisp", "Fog Wisp", EnemyKind.Sent, 450, 50, leak: 2, candy: 0, points: 0, size: 650);
            wisp.invisible = true; wisp.ability = EnemyAbility.FogAura; wisp.abilityIntervalTicks = 40;
            d.enemies.Add(wisp);
            var bomb = Enemy("send_bomb", "Bomb Beetle", EnemyKind.Sent, 550, 50, leak: 2, candy: 0, points: 0, size: 650);
            bomb.deathStunUnits = 2;
            d.enemies.Add(bomb);
            d.enemies.Add(Enemy("send_pixies", "Pixie", EnemyKind.Sent, 50, 95, leak: 1, candy: 0, points: 0, size: 380));

            // Send elements are spread so no single element counters most sends (balance pass Oct 2026).
            // PvP 2.0: candy price + income per wave. Cheap sends invest (more income), big sends hit hard (less income).
            d.sends.Add(Send("rat_rush", "Rat Rush", "send_rat", 3, 35, income: 6, element: Element.Grass, spacing: 10));
            d.sends.Add(Send("sneaky_ghost", "Sneaky Ghost", "send_ghost", 1, 50, income: 5, element: Element.Water));
            d.sends.Add(Send("shield_turtle", "Shield Turtle", "send_turtle", 1, 60, income: 4, element: Element.Fire));
            d.sends.Add(Send("candy_crow", "Candy Crow", "send_crow", 1, 65, income: 4, element: Element.Fire));
            d.sends.Add(Send("brick_golem", "Brick Golem", "send_golem", 1, 100, income: 2, element: Element.Water));
            d.sends.Add(Send("mini_king", "Mini Monster King", "send_king", 1, 160, income: 0, element: Element.Fire, once: true));
            d.sends.Add(Send("twin_slime", "Twin Slime", "send_splitter", 1, 70, income: 4, element: Element.Grass));
            d.sends.Add(Send("digger_mole", "Digger Mole", "send_mole", 1, 70, income: 3, element: Element.Water));
            d.sends.Add(Send("healer_shroom", "Healer Shroom", "send_mushroom", 1, 55, income: 4, element: Element.Grass));
            d.sends.Add(Send("fog_wisp", "Fog Wisp", "send_wisp", 1, 60, income: 4, element: Element.Light));
            d.sends.Add(Send("bomb_beetle", "Bomb Beetle", "send_bomb", 1, 75, income: 3, element: Element.Fire));
            d.sends.Add(Send("pixie_swarm", "Pixie Swarm", "send_pixies", 6, 45, income: 5, element: Element.Light, spacing: 6));
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
                new SpellDef { id = "poison_fog", displayName = "Poison Fog", kind = SpellKind.PoisonFog, cost = 40, cooldownTicks = 380, radius = 1300, value = 22, value2 = 25 },
                new SpellDef { id = "star_mark", displayName = "Star Mark", kind = SpellKind.StarMark, cost = 30, cooldownTicks = 400, radius = 1500, value = 30, value2 = 80 },
                new SpellDef { id = "shield_dome", displayName = "Shield Dome", kind = SpellKind.ShieldDome, cost = 40, cooldownTicks = 600, radius = 0, value = 120, value2 = 15 },
                new SpellDef { id = "lucky_star", displayName = "Lucky Star", kind = SpellKind.LuckyStar, cost = 70, cooldownTicks = 900, radius = 0 },
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
