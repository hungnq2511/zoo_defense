using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>
    /// Decorative widgets for the Home and Result screens. Everything is drawn in code and upgrades itself
    /// when optional art exists in Resources/ZooArt/UI (see Docs/PROMPTS_HOME_RESULT.md):
    ///   logo.png, ribbon.png, ribbon_defeat.png, podium.png, rays.png, button_battle.png
    /// </summary>
    public static class Deco
    {
        /// <summary>Banner ribbon with a title. Art "ribbon" / "ribbon_defeat" replaces the drawn ribbon.</summary>
        public static RectTransform Ribbon(Transform parent, string text, Color color, int fontSize, bool defeat = false)
        {
            var root = UIKit.Rect("ribbon", parent);
            var art = Art.Icon(defeat ? "ribbon_defeat" : "ribbon");
            if (art == null && defeat) art = null;
            if (art != null)
            {
                var img = UIKit.Icon(root, "art", art, Color.white);
                img.preserveAspect = false;
                img.rectTransform.Stretch(-40, -40, -20, -20);
            }
            else
            {
                var dark = Color.Lerp(color, Color.black, 0.4f);
                // Folded tails behind the band.
                for (int side = -1; side <= 1; side += 2)
                {
                    var tail = UIKit.Panel(root, "tail", dark).rectTransform;
                    tail.anchorMin = tail.anchorMax = new Vector2(side < 0 ? 0f : 1f, 0.5f);
                    tail.pivot = new Vector2(0.5f, 0.5f);
                    tail.sizeDelta = new Vector2(120, 0);
                    tail.anchoredPosition = new Vector2(side * 30, -18);
                    tail.sizeDelta = new Vector2(120, 90);
                    tail.localRotation = Quaternion.Euler(0f, 0f, side * -6f);
                }
                var band = UIKit.Panel(root, "band", color).rectTransform;
                band.Stretch();
                var shine = UIKit.Panel(band, "shine", new Color(1f, 1f, 1f, 0.2f)).rectTransform;
                shine.Stretch(14, 14, 8, 0);
                shine.anchorMin = new Vector2(0f, 0.55f);
                var edge = UIKit.Panel(band, "edge", Color.Lerp(color, Color.black, 0.25f)).rectTransform;
                edge.Band(false, 14, 0, 8);
            }
            var label = UIKit.Label(root, text, fontSize, Color.white);
            label.rectTransform.Stretch(30, 30, 0, 6);
            var o = label.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.2f, 0.1f, 0.05f, 0.85f);
            o.effectDistance = new Vector2(3f, -3f);
            return root;
        }

        /// <summary>Big title text with a thick outline (used when no logo art exists).</summary>
        public static Text Title(Transform parent, string text, int size, Color color)
        {
            var t = UIKit.Label(parent, text, size, color);
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = Art.Hex(0x3B2412);
            o.effectDistance = new Vector2(5f, -5f);
            var s = t.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.4f);
            s.effectDistance = new Vector2(0f, -10f);
            return t;
        }

        /// <summary>Game logo: art "logo" or the drawn title.</summary>
        public static RectTransform Logo(Transform parent)
        {
            var art = Art.Icon("logo");
            if (art != null) return UIKit.Icon(parent, "logo", art, Color.white).rectTransform;
            var root = UIKit.Rect("logo", parent);
            Title(root, "ZOO DEFENSE", 96, Art.Hex(0xFFD23F)).rectTransform.Stretch();
            return root;
        }

        /// <summary>Full-screen gradient overlay (top or bottom) to keep text readable over art.</summary>
        public static void Fade(Transform parent, bool top, float height, Color color)
        {
            var img = UIKit.Icon(parent, top ? "fade_top" : "fade_bottom", Art.GradientV, color);
            img.preserveAspect = false;
            img.rectTransform.Band(top, height);
            if (top) img.rectTransform.localScale = new Vector3(1f, -1f, 1f);
        }

        /// <summary>Progress toward the next arena: [arena icon] ===== 640 / 700 [next].</summary>
        public static Bar TrophyRoad(Transform parent, int trophies, out Text label)
        {
            var cur = MetaRules.ArenaFor(trophies);
            int next = -1;
            foreach (var a in MetaRules.Arenas) if (a.minTrophies > trophies) { next = a.minTrophies; break; }
            var bar = new Bar(parent, Palette.Primary, 30);
            float f = next < 0 ? 1f : (trophies - cur.minTrophies) / (float)Mathf.Max(1, next - cur.minTrophies);
            bar.Set(f, next < 0 ? Loc.T("lobby.top_arena") : trophies + " / " + next);
            var icon = Icons.Currency(bar.Root, "trophy");
            icon.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(78, 78));
            label = bar.Root.GetComponentInChildren<Text>();
            return bar;
        }

        /// <summary>Soft ellipse shadow under a character.</summary>
        public static Image GroundShadow(Transform parent, Vector2 pos, Vector2 size)
        {
            var img = UIKit.Icon(parent, "shadow", Art.Glow, new Color(0f, 0f, 0f, 0.45f));
            img.preserveAspect = false;
            img.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), pos, size);
            return img;
        }

        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        // ------------------------------------------------------------------ shared screen kit (Shop, Deck, Pass)

        /// <summary>Arena art (cover), tinted and darkened, with fades - the common backdrop of the menu tabs.</summary>
        public static void ScreenBackground(RectTransform root, GameApp app, Color tint, float artAlpha = 0.35f)
        {
            UIKit.Panel(root, "bg", Color.Lerp(tint, Palette.Background, 0.75f), false).rectTransform.Stretch();
            var arena = MetaRules.ArenaFor(app.Meta.State.trophies);
            var art = Art.Background(arena.index);
            if (art != null) UIKit.Cover(UIKit.Icon(root, "arena_art", art, new Color(1f, 1f, 1f, artAlpha)));
            UIKit.Panel(root, "wash", new Color(tint.r, tint.g, tint.b, 0.25f), false).rectTransform.Stretch();
            Fade(root, true, 520, new Color(0.08f, 0.05f, 0.14f, 0.9f));
            Fade(root, false, 520, new Color(0.08f, 0.05f, 0.14f, 0.9f));
        }

        /// <summary>Ribbon title under the currency bar. Returns the offset from the top where content can start.</summary>
        public static float Header(RectTransform root, string title, Color color)
        {
            Ribbon(root, title, color, 46).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -CurrencyBar.Height - 14), new Vector2(640, 96));
            return CurrencyBar.Height + 124;
        }

        /// <summary>
        /// Game card frame: coloured border, dark inner panel tinted from the top. Content goes into the returned
        /// rect (it fills the inner area). The frame object itself is the returned rect's parent.
        /// </summary>
        public static RectTransform Frame(Transform parent, string name, Color border, Color fill, float thickness = 7f)
        {
            var root = UIKit.Panel(parent, name, Color.Lerp(border, Color.black, 0.35f)).rectTransform;
            root.Stretch();   // fill the parent cell by default (callers may re-Place it)
            UIKit.Panel(root, "rim", border).rectTransform.Stretch(0, 0, 0, 5);
            var inner = UIKit.Panel(root, "inner", fill).rectTransform;
            inner.Stretch(thickness, thickness, thickness, thickness + 5);
            var tint = UIKit.Icon(inner, "tint", Art.GradientV, new Color(border.r, border.g, border.b, 0.45f));
            tint.preserveAspect = false;
            tint.rectTransform.Stretch(4, 4, 4, 0);
            tint.rectTransform.anchorMin = new Vector2(0f, 0.45f);
            tint.rectTransform.localScale = new Vector3(1f, -1f, 1f);
            var content = UIKit.Rect("content", root);
            content.Stretch(thickness, thickness, thickness, thickness + 5);
            return content;
        }

        /// <summary>Section title with lines on both sides: ---- DAILY DEALS ----</summary>
        public static void Section(Transform parent, string text, float y, Color color)
        {
            var row = UIKit.Rect("section", parent).Band(true, 64, y, 6);
            var label = UIKit.Label(row, text, 36, color);
            label.rectTransform.Stretch(150, 150, 0, 0);
            var o = label.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.6f);
            o.effectDistance = new Vector2(2f, -2f);
            for (int side = 0; side < 2; side++)
            {
                var line = UIKit.Panel(row, "line", new Color(color.r, color.g, color.b, 0.5f), false).rectTransform;
                line.anchorMin = new Vector2(side == 0 ? 0f : 0.86f, 0.5f);
                line.anchorMax = new Vector2(side == 0 ? 0.14f : 1f, 0.5f);
                line.sizeDelta = new Vector2(0, 4);
                line.anchoredPosition = Vector2.zero;
            }
        }

        /// <summary>Rounded dark pill with an icon on the left and a text, e.g. a price tag.</summary>
        public static RectTransform Pill(Transform parent, string icon, string text, int fontSize = 30)
        {
            var bg = UIKit.Panel(parent, "pill", new Color(0f, 0f, 0f, 0.55f)).rectTransform;
            if (!string.IsNullOrEmpty(icon))
                Icons.Currency(bg, icon).rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(10, 0), new Vector2(56, 56));
            UIKit.Label(bg, text, fontSize, Palette.Text).rectTransform.Stretch(string.IsNullOrEmpty(icon) ? 8 : 40, 8, 0, 0);
            return bg;
        }

        /// <summary>Tilted corner tag (e.g. "BEST VALUE").</summary>
        public static RectTransform Tag(Transform parent, string text, Color color)
        {
            var tag = UIKit.Panel(parent, "tag", color).rectTransform;
            tag.Place(new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(10, -4), new Vector2(190, 46));
            tag.localRotation = Quaternion.Euler(0f, 0f, 8f);
            UIKit.Label(tag, text, 22, Palette.Text).rectTransform.Stretch(4, 4, 0, 0);
            return tag;
        }

        /// <summary>Round green badge with a check mark (claimed / owned).</summary>
        public static RectTransform Check(Transform parent, float size)
        {
            var c = UIKit.Icon(parent, "check", Art.Circle, Palette.Green).rectTransform;
            c.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            // Tick drawn from two bars (the font has no check-mark glyph).
            var a = UIKit.Panel(c, "a", Color.white, false).rectTransform;
            a.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-size * 0.12f, -size * 0.06f), new Vector2(size * 0.3f, size * 0.12f));
            a.localRotation = Quaternion.Euler(0f, 0f, -45f);
            var b = UIKit.Panel(c, "b", Color.white, false).rectTransform;
            b.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(size * 0.08f, size * 0.04f), new Vector2(size * 0.5f, size * 0.12f));
            b.localRotation = Quaternion.Euler(0f, 0f, 50f);
            return c;
        }

        /// <summary>Padlock drawn from simple shapes.</summary>
        public static RectTransform Lock(Transform parent, float size)
        {
            var root = UIKit.Rect("lock", parent).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            UIKit.Icon(root, "shackle", Art.Ring, Art.Hex(0xE6DEF5)).rectTransform
                .Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(size * 0.62f, size * 0.62f));
            var body = UIKit.Panel(root, "body", Art.Hex(0xE6DEF5)).rectTransform;
            body.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(size * 0.8f, size * 0.55f));
            UIKit.Icon(body, "hole", Art.Circle, Art.Hex(0x3A2D57)).rectTransform
                .Place(new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.16f, size * 0.16f));
            return root;
        }
    }

    /// <summary>
    /// The player's 5 battle animals standing on a podium, idling with a little bob. The middle (first deck
    /// slot) is the "hero". Tapping opens the Deck tab.
    /// </summary>
    public sealed class Showcase
    {
        readonly List<RectTransform> _units = new List<RectTransform>();
        readonly List<float> _phase = new List<float>();
        readonly List<Vector2> _base = new List<Vector2>();
        float _t;
        public RectTransform Root { get; private set; }

        public Showcase(Transform parent, GameApp app, float width, float height)
        {
            Root = UIKit.Rect("showcase", parent);
            Root.sizeDelta = new Vector2(width, height);

            // Glow + podium
            var glow = UIKit.Icon(Root, "glow", Art.Glow, new Color(1f, 0.93f, 0.6f, 0.55f));
            glow.preserveAspect = false;
            glow.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(width * 1.05f, height * 0.95f));
            var podiumArt = Art.Icon("podium");
            if (podiumArt != null)
            {
                var p = UIKit.Icon(Root, "podium", podiumArt, Color.white);
                p.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, -10), new Vector2(width * 0.98f, height * 0.36f));
            }
            else
            {
                var rim = UIKit.Icon(Root, "podium_rim", Art.Circle, Art.Hex(0x7A4E2D));
                rim.preserveAspect = false;
                rim.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 0), new Vector2(width * 0.96f, 150));
                var top = UIKit.Icon(Root, "podium_top", Art.Circle, Art.Hex(0x8FD06A));
                top.preserveAspect = false;
                top.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 22), new Vector2(width * 0.92f, 128));
                var hi = UIKit.Icon(Root, "podium_hi", Art.Glow, new Color(1f, 1f, 1f, 0.35f));
                hi.preserveAspect = false;
                hi.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(width * 0.7f, 80));
            }

            // Back row first (smaller, higher), hero last so it draws on top.
            var deck = app.Profile.deck;
            int[] order = { 3, 4, 1, 2, 0 };
            float[] xs = { -0.36f, 0.36f, -0.21f, 0.21f, 0f };
            float[] ys = { 100f, 100f, 70f, 70f, 42f };
            float[] sz = { 0.40f, 0.40f, 0.46f, 0.46f, 0.64f };
            for (int k = 0; k < order.Length; k++)
            {
                int i = order[k];
                if (i >= deck.Length) continue;
                var def = app.Content.Unit(deck[i]);
                if (def == null) continue;
                float size = height * sz[k];
                var pos = new Vector2(width * xs[k], ys[k]);
                Deco.GroundShadow(Root, pos + new Vector2(0, 6), new Vector2(size * 0.75f, size * 0.18f));
                var holder = UIKit.Rect("unit_" + def.id, Root).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0.1f), pos, new Vector2(size, size));
                int level = Mathf.Max(1, app.Meta.Level(def.id));
                // Show the "evolved" look for well-upgraded cards: card level 5+ = level-4 art, 10+ = level-7 art.
                Icons.Unit(holder, def, level >= 10 ? 7 : (level >= 5 ? 4 : 1), size).Stretch();
                if (i == 0)
                {
                    // Only the hero shows its card level (the others would overlap on the podium).
                    var lv = UIKit.Panel(holder, "lv", new Color(0f, 0f, 0f, 0.6f));
                    lv.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, 4), new Vector2(110, 38));
                    UIKit.Label(lv.transform, Loc.F("deck.level_short", level), 24, Palette.Text).rectTransform.Stretch();
                }
                _units.Add(holder);
                _phase.Add(k * 1.37f);
                _base.Add(pos);
            }

            var hit = Root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            var btn = Root.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => { AudioService.Play(Sfx.Click); app.Show(new DeckScreen()); });
        }

        public void Tick(float dt)
        {
            _t += dt;
            for (int i = 0; i < _units.Count; i++)
            {
                float s = Mathf.Sin(_t * 2.4f + _phase[i]);
                _units[i].anchoredPosition = _base[i] + new Vector2(0f, Mathf.Max(0f, s) * 10f);
                float squash = 1f + 0.03f * s;
                _units[i].localScale = new Vector3(2f - squash, squash, 1f);
            }
        }
    }
}
