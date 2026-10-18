using System;
using System.Collections.Generic;
using UnityEngine;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>
    /// Local save. The economy part (currencies, cards, chests, missions, pass) lives in <see cref="meta"/>,
    /// which the Core MetaService owns; later it moves to UGS Cloud Save with the server as authority.
    /// </summary>
    [Serializable]
    public class PlayerProfile
    {
        public int version = 2;
        public string playerName = "Player";
        public int matchesPlayed;
        public int wins;
        public int botDifficulty = 1;
        /// <summary>"en", "vi", or "" to follow the device language.</summary>
        public string language = "";
        public string[] deck = (string[])DefaultContent.DefaultDeck.Clone();
        public string[] sendDeck = (string[])DefaultContent.DefaultSendDeck.Clone();
        /// <summary>4 keeper spells taken into battle.</summary>
        public string[] spells = (string[])DefaultContent.DefaultSpells.Clone();
        public bool tutorialDone;
        public float musicVolume = 0.6f;
        public float sfxVolume = 0.8f;
        /// <summary>Test-only clock offset (seconds) set from the debug section of Settings.</summary>
        public long debugTimeOffset;
        public PlayerState meta = new PlayerState();

        // Version 1 fields, migrated into meta on load.
        public int trophies;
        public int gold;
    }

    public static class ProfileStore
    {
        const string Key = "zoo_profile_v1";

        public static PlayerProfile Load(ContentDatabase content, MetaConfig meta)
        {
            PlayerProfile p = null;
            var json = PlayerPrefs.GetString(Key, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { p = JsonUtility.FromJson<PlayerProfile>(json); }
                catch (Exception e) { Debug.LogWarning("Profile reset, could not parse: " + e.Message); }
            }
            if (p == null) p = new PlayerProfile { playerName = "Player" + UnityEngine.Random.Range(1000, 9999) };
            Sanitize(p, content, meta);
            return p;
        }

        public static void Save(PlayerProfile p)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(p));
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        static void Sanitize(PlayerProfile p, ContentDatabase content, MetaConfig meta)
        {
            if (p.meta == null || p.meta.cards == null || p.meta.cards.Count == 0)
            {
                p.meta = MetaService.NewPlayer(meta, content, UnityEngine.Random.Range(1, int.MaxValue));
                // Carry over progress from version 1 saves.
                p.meta.trophies = Mathf.Max(0, p.trophies);
                p.meta.gold += Mathf.Max(0, p.gold);
                if (p.matchesPlayed > 0) p.tutorialDone = true;
            }
            p.trophies = 0;
            p.gold = 0;
            p.version = 2;

            var service = new MetaService(meta, content, p.meta);
            if (!ValidDeck(p.deck, 5, id => content.UnitIndex(id) >= 0 && service.IsUnlocked(id))) p.deck = (string[])DefaultContent.DefaultDeck.Clone();
            if (!ValidDeck(p.sendDeck, 4, id => content.SendIndex(id) >= 0)) p.sendDeck = (string[])DefaultContent.DefaultSendDeck.Clone();
            if (!ValidDeck(p.spells, 4, id => content.SpellIndex(id) >= 0)) p.spells = (string[])DefaultContent.DefaultSpells.Clone();
            p.botDifficulty = Mathf.Clamp(p.botDifficulty, 0, 2);
            p.musicVolume = Mathf.Clamp01(p.musicVolume);
            p.sfxVolume = Mathf.Clamp01(p.sfxVolume);
            Language lang;
            if (!Loc.TryParse(p.language, out lang)) p.language = "";
        }

        static bool ValidDeck(string[] deck, int size, Func<string, bool> ok)
        {
            if (deck == null || deck.Length != size) return false;
            var seen = new HashSet<string>();
            foreach (var id in deck) if (!ok(id) || !seen.Add(id)) return false;
            return true;
        }
    }

    public struct Arena
    {
        public int index;
        public string name;
        public int minTrophies;
        public Color tint;
    }

    public static class MetaRules
    {
        public static readonly Arena[] Arenas =
        {
            new Arena { index = 1, name = "Grassy Meadow", minTrophies = 0, tint = new Color(0.36f, 0.62f, 0.33f) },
            new Arena { index = 2, name = "Deep Forest", minTrophies = 300, tint = new Color(0.20f, 0.45f, 0.30f) },
            new Arena { index = 3, name = "Volcano", minTrophies = 700, tint = new Color(0.55f, 0.30f, 0.24f) },
            new Arena { index = 4, name = "Ice Island", minTrophies = 1200, tint = new Color(0.40f, 0.60f, 0.72f) },
            new Arena { index = 5, name = "Cloud Palace", minTrophies = 1800, tint = new Color(0.62f, 0.56f, 0.78f) },
        };

        public static Arena ArenaFor(int trophies)
        {
            var a = Arenas[0];
            foreach (var x in Arenas) if (trophies >= x.minTrophies) a = x;
            return a;
        }

        /// <summary>Bot card levels around the player's average so matches stay fair (matchmaking by deck power).</summary>
        public static int[] BotCardLevels(int[] playerLevels, int count, int seed)
        {
            int sum = 0;
            foreach (var l in playerLevels) sum += l;
            int avg = playerLevels.Length > 0 ? Mathf.RoundToInt((float)sum / playerLevels.Length) : 1;
            var rng = new DetRandom(seed);
            var levels = new int[count];
            for (int i = 0; i < count; i++) levels[i] = Mathf.Clamp(avg - 1 + rng.Next(3), 1, 15);
            return levels;
        }

        static readonly string[] BotNames = { "Gojiraaaa", "Cujel", "MochiPaws", "TacoTiger", "NightOwlz", "BubbleKing", "SirHops", "Pandamonium", "FoxyLady", "LeafStorm" };

        /// <summary>4 random keeper spells for a bot.</summary>
        public static string[] BotSpells(ContentDatabase content, int seed)
        {
            var pool = new List<string>();
            foreach (var s in content.Spells) pool.Add(s.id);
            var rng = new DetRandom(seed ^ 0x51ed);
            var result = new List<string>();
            while (result.Count < 4 && pool.Count > 0) { int i = rng.Next(pool.Count); result.Add(pool[i]); pool.RemoveAt(i); }
            return result.ToArray();
        }

        public static string RandomBotName(int seed) { return BotNames[Mathf.Abs(seed) % BotNames.Length]; }
    }
}
