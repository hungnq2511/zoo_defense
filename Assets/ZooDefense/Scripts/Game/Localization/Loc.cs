using System;
using System.Collections.Generic;
using UnityEngine;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    public enum Language { English = 0, Vietnamese = 1 }

    /// <summary>
    /// Lightweight localization for the code-built UI. Strings live in LocTables.cs (one table per language).
    /// Missing keys fall back to English, then to the key itself, so a missing translation never breaks the UI.
    /// Later this can move to the Unity Localization package or Remote Config without changing call sites.
    /// </summary>
    public static class Loc
    {
        static readonly Dictionary<string, string>[] _tables = { LocTables.English(), LocTables.Vietnamese() };

        public static Language Current { get; private set; }
        public static event Action Changed;

        public static readonly Language[] Supported = { Language.English, Language.Vietnamese };

        /// <summary>code: "en", "vi", or "" for the device language.</summary>
        public static void Init(string code)
        {
            Language lang;
            Current = TryParse(code, out lang) ? lang : FromSystem();
        }

        public static void Set(Language lang)
        {
            if (lang == Current) return;
            Current = lang;
            if (Changed != null) Changed();
        }

        public static string Code(Language lang) { return lang == Language.Vietnamese ? "vi" : "en"; }

        /// <summary>The language's own name, shown on the language button.</summary>
        public static string NativeName(Language lang) { return lang == Language.Vietnamese ? "Tiếng Việt" : "English"; }

        public static bool TryParse(string code, out Language lang)
        {
            lang = Language.English;
            if (string.IsNullOrEmpty(code)) return false;
            if (code == "vi") { lang = Language.Vietnamese; return true; }
            if (code == "en") { lang = Language.English; return true; }
            return false;
        }

        static Language FromSystem()
        {
            return Application.systemLanguage == SystemLanguage.Vietnamese ? Language.Vietnamese : Language.English;
        }

        public static string T(string key)
        {
            string s;
            if (_tables[(int)Current].TryGetValue(key, out s)) return s;
            if (_tables[0].TryGetValue(key, out s)) return s;
            return key;
        }

        public static string F(string key, params object[] args)
        {
            try { return string.Format(T(key), args); }
            catch (FormatException) { return T(key); }
        }

        /// <summary>Keys present in English but missing in the given language (for the editor check and tests).</summary>
        public static List<string> MissingKeys(Language lang)
        {
            var missing = new List<string>();
            foreach (var key in _tables[0].Keys) if (!_tables[(int)lang].ContainsKey(key)) missing.Add(key);
            missing.Sort();
            return missing;
        }

        // ------------------------------------------------------------------ game content names

        public static string UnitName(UnitDef d) { return Name("unit." + d.id, d.displayName); }
        public static string EnemyName(EnemyDef d) { return Name("enemy." + d.id, d.displayName); }
        public static string SendName(SendDef d) { return Name("send." + d.id, d.displayName); }
        public static string ArenaName(Arena a) { return T("arena." + a.index); }
        public static string ElementName(Element e) { return T("element." + e.ToString().ToLowerInvariant()); }
        public static string RoleName(Role r) { return T("role." + r.ToString().ToLowerInvariant()); }
        public static string RarityName(Rarity r) { return T("rarity." + r.ToString().ToLowerInvariant()); }

        public static string SkillText(UnitDef d)
        {
            if (d.skill == UnitSkill.None) return "";
            return F("skill." + d.skill.ToString().ToLowerInvariant(), d.skillValue, d.skillValue2);
        }

        public static string RejectText(RejectReason r)
        {
            switch (r)
            {
                case RejectReason.NotEnoughCandy: return T("reject.candy");
                case RejectReason.NotEnoughPoints: return T("reject.points");
                case RejectReason.BoardFull: return T("reject.board_full");
                case RejectReason.CellLocked: return T("reject.locked");
                case RejectReason.OpponentWaveFull: return T("reject.wave_full");
                case RejectReason.AlreadyUsed: return T("reject.used");
                case RejectReason.MaxLevel: return T("reject.max_level");
                case RejectReason.Cooldown: return T("reject.cooldown");
                case RejectReason.NotReady: return T("reject.not_ready");
                case RejectReason.NoTarget: return T("reject.no_target");
                case RejectReason.InvalidCell: return T("reject.invalid_cell");
                case RejectReason.NotOnBoard: return T("reject.not_on_board");
                default: return T("reject.generic");
            }
        }

        /// <summary>Content names exist in both tables; content added later without a translation uses its English name.</summary>
        static string Name(string key, string fallback)
        {
            string s;
            if (_tables[(int)Current].TryGetValue(key, out s)) return s;
            return fallback;
        }
    }
}
