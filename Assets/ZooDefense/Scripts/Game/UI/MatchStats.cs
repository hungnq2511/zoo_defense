using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>
    /// Post-match stats: damage per animal for both players (MVP first), spell damage and what the sends achieved.
    /// The numbers come from replaying the match record, so it also works for saved replays.
    /// </summary>
    public static class MatchStats
    {
        sealed class Row
        {
            public UnitDef def;     // null = keeper spells
            public long damage;
        }

        sealed class Side
        {
            public string name;
            public bool local;
            public long total;
            public int heartsDealt, merges, spells;
            public readonly List<Row> rows = new List<Row>();
        }

        public static void Show(GameApp app, MatchRecord record, int localPlayer)
        {
            if (record == null || record.setup == null) return;
            Match m;
            try { m = ReplayValidator.Replay(app.Content, record); }
            catch (Exception e)
            {
                Debug.LogWarning("[ZooDefense] stats replay failed: " + e.Message);
                app.Toast(Loc.T("stats.none"));
                return;
            }

            bool solo = record.setup.rules != null && record.setup.rules.solo;
            localPlayer = Mathf.Clamp(localPlayer, 0, 1);
            var sides = new List<Side> { Read(m, localPlayer, true) };
            if (!solo) sides.Add(Read(m, 1 - localPlayer, false));
            long best = 1;
            foreach (var s in sides) foreach (var r in s.rows) best = Math.Max(best, r.damage);

            var box = Modal.Open(app, Loc.T("stats.title"), 1560);
            var area = UIKit.Rect("area", box).Stretch(20, 20, 120, 30);
            var content = ScrollKit.Vertical(area);
            float y = 0f;
            for (int i = 0; i < sides.Count; i++) y = Section(content, sides[i], best, y, i == 0);
            ScrollKit.SetHeight(content, y + 20f);
        }

        static Side Read(Match m, int player, bool local)
        {
            var b = m.Boards[player];
            var s = new Side
            {
                name = b.PlayerName, local = local, heartsDealt = b.HeartsDealt, merges = b.Merges, spells = b.SpellsCast,
            };
            for (int i = 0; i < b.DamageByUnit.Length; i++)
                if (b.DamageByUnit[i] > 0) s.rows.Add(new Row { def = m.Content.Units[i], damage = b.DamageByUnit[i] });
            if (b.SpellDamage > 0) s.rows.Add(new Row { def = null, damage = b.SpellDamage });
            s.rows.Sort((x, y) => y.damage.CompareTo(x.damage));
            foreach (var r in s.rows) s.total += r.damage;
            return s;
        }

        static float Section(RectTransform content, Side s, long best, float y, bool first)
        {
            if (!first) y += 30f;
            var head = UIKit.Rect("head", content).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -y), new Vector2(900, 120));
            string who = s.local ? Loc.F("stats.you", s.name) : s.name;
            UIKit.Outlined(UIKit.Label(head, who, 40, s.local ? Palette.Primary : Palette.TextDim, TextAnchor.UpperLeft), 2f).rectTransform.Stretch(10, 300, 0, 56);
            UIKit.Label(head, Loc.F("stats.total", Compact(s.total)), 30, Palette.Text, TextAnchor.UpperRight).rectTransform.Stretch(300, 10, 6, 60);
            UIKit.Label(head, Loc.F("stats.line", s.heartsDealt, s.merges, s.spells), 26, Palette.TextDim, TextAnchor.LowerLeft).rectTransform.Stretch(10, 10, 64, 6);
            y += 130f;

            if (s.rows.Count == 0)
            {
                UIKit.Label(content, Loc.T("stats.none"), 30, Palette.TextDim).rectTransform
                    .Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -y), new Vector2(900, 80));
                return y + 90f;
            }

            for (int i = 0; i < s.rows.Count; i++)
            {
                var r = s.rows[i];
                bool mvp = i == 0;
                float h = mvp ? 150f : 116f;
                var row = UIKit.Panel(content, "row" + i, mvp ? new Color(1f, 0.75f, 0.2f, 0.18f) : new Color(0f, 0f, 0f, 0.28f)).rectTransform;
                row.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -y), new Vector2(900, h - 10f));

                float icon = mvp ? 124f : 92f;
                var holder = UIKit.Rect("icon", row).Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12, 0), new Vector2(icon, icon));
                if (r.def != null) Icons.Unit(holder, r.def, 1, icon).Stretch();
                else UIKit.Icon(holder, "spell", Art.Diamond, Palette.Purple).rectTransform.Stretch(14, 14, 14, 14);

                float left = 24f + icon;
                string name = r.def != null ? Loc.UnitName(r.def) : Loc.T("stats.spells");
                UIKit.Label(row, name, mvp ? 34 : 30, Palette.Text, TextAnchor.UpperLeft).rectTransform.Stretch(left, 230, 10, h * 0.5f);
                if (mvp)
                {
                    var tag = UIKit.Panel(row, "mvp", Palette.Primary).rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12, -10), new Vector2(110, 44));
                    UIKit.Outlined(UIKit.Label(tag, Loc.T("stats.mvp"), 28, Palette.Text), 1.5f).rectTransform.Stretch();
                }

                int pct = s.total > 0 ? (int)Math.Round(r.damage * 100.0 / s.total) : 0;
                var bar = new Bar(row, r.def != null ? Art.ElementColor(r.def.element) : Palette.Purple, 24);
                bar.Root.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(left, 14), new Vector2(880f - left - 130f, 42));
                bar.Set(r.damage / (float)best, Compact(r.damage));
                UIKit.Label(row, pct + "%", 34, mvp ? Palette.Primary : Palette.Text, TextAnchor.LowerRight).rectTransform.Stretch(0, 16, h * 0.4f, 10);
                y += h;
            }
            return y;
        }

        /// <summary>12345 -> 12.3K, 1234567 -> 1.2M.</summary>
        public static string Compact(long v)
        {
            if (v >= 1000000) return (v / 100000 / 10f).ToString("0.#") + "M";
            if (v >= 10000) return (v / 100 / 10f).ToString("0.#") + "K";
            return v.ToString();
        }
    }
}
