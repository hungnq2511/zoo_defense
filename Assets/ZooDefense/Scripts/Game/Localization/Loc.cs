using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>Values are stored in the profile by code (Loc.Code), so the numbers can change freely.</summary>
    public enum Language { English = 0, Vietnamese = 1, Japanese = 2, Korean = 3, German = 4, French = 5, Spanish = 6, Portuguese = 7 }

    /// <summary>
    /// Lightweight localization for the code-built UI. English and Vietnamese live in LocTables.cs; the other
    /// languages are tab-separated files in Resources/ZooLoc/{code}.txt (key, tab, text; "\n" = new line), loaded
    /// the first time they are used. Missing keys fall back to English, then to the key itself, so a missing
    /// translation never breaks the UI.
    /// </summary>
    public static class Loc
    {
        const int Count = 8;
        static readonly Dictionary<string, string>[] _tables = new Dictionary<string, string>[Count];

        public static Language Current { get; private set; }
        public static event Action Changed;

        /// <summary>Order shown in the language picker.</summary>
        public static readonly Language[] Supported =
        {
            Language.English, Language.Spanish, Language.Portuguese, Language.French, Language.German,
            Language.Japanese, Language.Korean, Language.Vietnamese,
        };

        /// <summary>code: "en", "ja", … or "" for the device language.</summary>
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

        public static string Code(Language lang)
        {
            switch (lang)
            {
                case Language.Vietnamese: return "vi";
                case Language.Japanese: return "ja";
                case Language.Korean: return "ko";
                case Language.German: return "de";
                case Language.French: return "fr";
                case Language.Spanish: return "es";
                case Language.Portuguese: return "pt";
                default: return "en";
            }
        }

        /// <summary>The language's own name, shown in the language picker.</summary>
        public static string NativeName(Language lang)
        {
            switch (lang)
            {
                case Language.Vietnamese: return "Tiếng Việt";
                case Language.Japanese: return "日本語";
                case Language.Korean: return "한국어";
                case Language.German: return "Deutsch";
                case Language.French: return "Français";
                case Language.Spanish: return "Español";
                case Language.Portuguese: return "Português";
                default: return "English";
            }
        }

        public static bool TryParse(string code, out Language lang)
        {
            lang = Language.English;
            if (string.IsNullOrEmpty(code)) return false;
            code = code.ToLowerInvariant();
            int dash = code.IndexOfAny(new[] { '-', '_' });
            if (dash > 0) code = code.Substring(0, dash);
            for (int i = 0; i < Count; i++)
            {
                if (Code((Language)i) != code) continue;
                lang = (Language)i;
                return true;
            }
            return false;
        }

        static Language FromSystem()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Vietnamese: return Language.Vietnamese;
                case SystemLanguage.Japanese: return Language.Japanese;
                case SystemLanguage.Korean: return Language.Korean;
                case SystemLanguage.German: return Language.German;
                case SystemLanguage.French: return Language.French;
                case SystemLanguage.Spanish: return Language.Spanish;
                case SystemLanguage.Portuguese: return Language.Portuguese;
                default: return Language.English;
            }
        }

        /// <summary>The strings of one language (loaded on first use; empty if its file is missing).</summary>
        public static Dictionary<string, string> Table(Language lang)
        {
            int i = (int)lang;
            if (i < 0 || i >= Count) i = 0;
            if (_tables[i] != null) return _tables[i];
            if (lang == Language.English) _tables[i] = LocTables.English();
            else if (lang == Language.Vietnamese) _tables[i] = LocTables.Vietnamese();
            else _tables[i] = LoadFile(Code(lang));
            return _tables[i];
        }

        static Dictionary<string, string> LoadFile(string code)
        {
            var asset = Resources.Load<TextAsset>("ZooLoc/" + code);
            if (asset == null)
            {
                Debug.LogWarning("[Loc] Resources/ZooLoc/" + code + ".txt not found: using English.");
                return new Dictionary<string, string>();
            }
            return Parse(asset.text);
        }

        /// <summary>key TAB text per line; lines starting with # are comments. \n, \t, \" and \\ are unescaped.</summary>
        public static Dictionary<string, string> Parse(string text)
        {
            var table = new Dictionary<string, string>(800);
            if (string.IsNullOrEmpty(text)) return table;
            int start = 0;
            while (start < text.Length)
            {
                int end = text.IndexOf('\n', start);
                if (end < 0) end = text.Length;
                int len = end - start;
                if (len > 0 && text[end - 1] == '\r') len--;
                if (len > 0 && text[start] != '#')
                {
                    int tab = text.IndexOf('\t', start, len);
                    if (tab > start) table[text.Substring(start, tab - start)] = Unescape(text.Substring(tab + 1, start + len - tab - 1));
                }
                start = end + 1;
            }
            return table;
        }

        static string Unescape(string s)
        {
            if (s.IndexOf('\\') < 0) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char n = s[++i];
                    if (n == 'n') sb.Append('\n');
                    else if (n == 't') sb.Append('\t');
                    else sb.Append(n);   // \" -> ", \\ -> \
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        public static string T(string key)
        {
            string s;
            if (Table(Current).TryGetValue(key, out s)) return s;
            if (Table(Language.English).TryGetValue(key, out s)) return s;
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
            var table = Table(lang);
            foreach (var key in Table(Language.English).Keys) if (!table.ContainsKey(key)) missing.Add(key);
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

        /// <summary>Content names come from the language table; content added later without a translation uses its English name.</summary>
        static string Name(string key, string fallback)
        {
            string s;
            if (Table(Current).TryGetValue(key, out s)) return s;
            return fallback;
        }
    }
}
