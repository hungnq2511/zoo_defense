using UnityEngine;
using UnityEngine.UI;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>
    /// "How elements work" popup: the Fire / Grass / Water triangle drawn with arrows, Light, the exact
    /// multipliers from the balance config, how to read a monster's element and the wave element.
    /// Opened from the battle HUD (wave pill), the pause menu and the deck screen.
    /// </summary>
    public static class ElementGuide
    {
        const string SeenKey = "zoo.element_guide_seen";

        public static bool Seen
        {
            get { try { return PlayerPrefs.GetInt(SeenKey, 0) == 1; } catch { return true; } }
        }

        /// <summary>Sample animal shown inside each element orb.</summary>
        static string SampleUnit(Element e)
        {
            switch (e) { case Element.Fire: return "fire_fox"; case Element.Water: return "bubble_frog"; case Element.Grass: return "carrot_bunny"; default: return "sun_sparrow"; }
        }

        /// <summary>Optional element emblem art (ZooArt/UI/element_{fire|water|grass|light}).</summary>
        public static Sprite Emblem(Element e) { return Art.Icon("element_" + e.ToString().ToLowerInvariant()); }

        public static void Open(GameApp app)
        {
            try { PlayerPrefs.SetInt(SeenKey, 1); } catch { }
            var b = app.Content.Balance;
            var box = Modal.Open(app, Loc.T("guide.title"), 1640f);

            // ---- Triangle: Fire (top) -> Grass (bottom right) -> Water (bottom left) -> Fire
            var diagram = UIKit.Rect("triangle", box).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -78), new Vector2(880, 600));
            var fire = new Vector2(0, -150);
            var grass = new Vector2(250, -470);
            var water = new Vector2(-250, -470);
            string bonus = "+" + b.elementBonusPercent + "%";
            Arrow(diagram, fire, grass, Art.ElementColor(Element.Fire), bonus);
            Arrow(diagram, grass, water, Art.ElementColor(Element.Grass), bonus);
            Arrow(diagram, water, fire, Art.ElementColor(Element.Water), bonus);
            Orb(app, diagram, Element.Fire, fire, 190);
            Orb(app, diagram, Element.Grass, grass, 190);
            Orb(app, diagram, Element.Water, water, 190);
            var hint = UIKit.Label(diagram, Loc.T("guide.arrow_hint"), 24, Palette.TextDim);
            hint.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -330), new Vector2(330, 90));

            // ---- Multipliers
            float y = -700f;
            Rule(box, y, Palette.Green, "x" + Mult(100 + b.elementBonusPercent), Loc.F("guide.effective", b.elementBonusPercent)); y -= 96f;
            Rule(box, y, Palette.Red, "x" + Mult(100 - b.elementResistPercent), Loc.F("guide.resisted", b.elementResistPercent)); y -= 96f;
            Rule(box, y, Art.Hex(0x8A7FA6), "x1", Loc.T("guide.neutral")); y -= 110f;

            // ---- Light
            var light = Deco.Frame(box, "light", Art.ElementColor(Element.Light), Art.Hex(0x3A2D57), 6f);
            ((RectTransform)light.parent).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(860, 170));
            Orb(app, light, Element.Light, new Vector2(-340, 0), 130, true);
            var lt = UIKit.Label(light, Loc.F("guide.light", b.lightVsBigPercent), 27, Palette.Text, TextAnchor.MiddleLeft);
            lt.rectTransform.Stretch(180, 16, 6, 6);
            y -= 196f;

            // ---- Reading the board
            Section(box, y, Loc.T("guide.read_title")); y -= 60f;
            var ring = UIKit.Rect("ring", box).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-370, y - 46), new Vector2(84, 84));
            UIKit.Icon(ring, "rim", Art.Circle, Art.Hex(0x2B2140)).rectTransform.Stretch();
            UIKit.Icon(ring, "dot", Art.Circle, Art.ElementColor(Element.Water)).rectTransform.Stretch(14, 14, 14, 14);
            UIKit.Label(box, Loc.T("guide.ring"), 26, Palette.Text, TextAnchor.MiddleLeft).rectTransform
                .Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(60, y - 46), new Vector2(740, 92));
            y -= 104f;
            var chip = UIKit.Rect("chip", box).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-370, y - 46), new Vector2(110, 60));
            WaveChip.Draw(chip, Element.Grass, Element.Fire);
            UIKit.Label(box, Loc.F("guide.wave", b.waveElementPercent), 26, Palette.Text, TextAnchor.MiddleLeft).rectTransform
                .Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(60, y - 46), new Vector2(740, 92));
            y -= 112f;

            // ---- Tips
            var tips = UIKit.Label(box, Loc.T("guide.tips"), 25, Art.Hex(0xFFE7A8), TextAnchor.UpperLeft);
            tips.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(860, 150));
        }

        static string Mult(int percent)
        {
            return percent % 100 == 0 ? (percent / 100).ToString() : (percent / 100f).ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture);
        }

        static void Orb(GameApp app, RectTransform parent, Element e, Vector2 pos, float size, bool leftMiddle = false)
        {
            var anchor = leftMiddle ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 1f);
            var root = UIKit.Rect("orb_" + e, parent).Place(anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            var color = Art.ElementColor(e);
            var glow = UIKit.Icon(root, "glow", Art.Glow, new Color(color.r, color.g, color.b, 0.55f));
            glow.rectTransform.Stretch(-size * 0.25f, -size * 0.25f, -size * 0.25f, -size * 0.25f);
            UIKit.Icon(root, "rim", Art.Circle, Art.Hex(0x3B2412)).rectTransform.Stretch();
            UIKit.Icon(root, "fill", Art.Circle, color).rectTransform.Stretch(6, 6, 6, 6);
            UIKit.Icon(root, "shine", Art.Circle, new Color(1f, 1f, 1f, 0.25f)).rectTransform
                .Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-size * 0.12f, -size * 0.1f), new Vector2(size * 0.45f, size * 0.3f));
            var emblem = Emblem(e);
            if (emblem != null)
                UIKit.Icon(root, "emblem", emblem, Color.white).rectTransform.Stretch(size * 0.14f, size * 0.14f, size * 0.14f, size * 0.14f);
            else
            {
                var def = app.Content.Unit(SampleUnit(e));
                if (def != null) Icons.Unit(root, def, 1, size * 0.8f).Stretch(size * 0.1f, size * 0.1f, size * 0.06f, size * 0.14f);
            }
            var name = UIKit.Outlined(UIKit.Label(root, Loc.ElementName(e), leftMiddle ? 26 : 34, Palette.Text), 2.5f);
            name.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, 8), new Vector2(240, 46));
            if (!leftMiddle) UIKit.Motion(root).Bob(4f, 1.6f + (int)e * 0.2f);
        }

        /// <summary>Thick arrow from orb a to orb b (stops at the orb edges) with the bonus written on it.</summary>
        static void Arrow(RectTransform parent, Vector2 a, Vector2 b, Color color, string label)
        {
            var dir = (b - a).normalized;
            var start = a + dir * 118f;
            var end = b - dir * 128f;
            float len = (end - start).magnitude;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            var shaft = UIKit.Panel(parent, "arrow", Art.Hex(0x3B2412), false).rectTransform;
            shaft.anchorMin = shaft.anchorMax = new Vector2(0.5f, 1f);
            shaft.pivot = new Vector2(0f, 0.5f);
            shaft.anchoredPosition = start;
            shaft.sizeDelta = new Vector2(len - 20f, 24f);
            shaft.localRotation = Quaternion.Euler(0f, 0f, angle);
            var inner = UIKit.Panel(shaft, "fill", color, false).rectTransform;
            inner.Stretch(0, 0, 5, 5);
            var head = UIKit.Icon(parent, "head", Art.Diamond, Art.Hex(0x3B2412)).rectTransform;
            head.anchorMin = head.anchorMax = new Vector2(0.5f, 1f);
            head.pivot = new Vector2(0.5f, 0.5f);
            head.anchoredPosition = end - dir * 18f;
            head.sizeDelta = new Vector2(64, 64);
            var headFill = UIKit.Icon(head, "fill", Art.Diamond, color).rectTransform;
            headFill.Stretch(7, 7, 7, 7);
            var mid = (start + end) * 0.5f;
            var normal = new Vector2(-dir.y, dir.x);
            if (normal.y < 0f) normal = -normal;   // label above / outside the line
            var tag = UIKit.Panel(parent, "bonus", Art.Hex(0x2E7D4A)).rectTransform;
            tag.anchorMin = tag.anchorMax = new Vector2(0.5f, 1f);
            tag.pivot = new Vector2(0.5f, 0.5f);
            tag.anchoredPosition = mid + normal * 44f;
            tag.sizeDelta = new Vector2(110, 50);
            UIKit.Label(tag, label, 28, Palette.Text).rectTransform.Stretch();
        }

        static void Rule(RectTransform box, float y, Color color, string mult, string text)
        {
            var row = UIKit.Panel(box, "rule", new Color(0f, 0f, 0f, 0.28f)).rectTransform
                .Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(860, 84));
            var badge = UIKit.Panel(row, "mult", color).rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10, 0), new Vector2(130, 64));
            UIKit.Outlined(UIKit.Label(badge, mult, 32, Palette.Text), 2f).rectTransform.Stretch();
            UIKit.Label(row, text, 26, Palette.Text, TextAnchor.MiddleLeft).rectTransform.Stretch(160, 12, 0, 0);
        }

        static void Section(RectTransform box, float y, string text)
        {
            var t = UIKit.Label(box, text, 30, Palette.Primary, TextAnchor.MiddleLeft);
            t.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(860, 50));
        }

        /// <summary>Very short matchup for small chips ("Beats Grass · Weak Water").</summary>
        public static string ShortLine(UnitDef d)
        {
            if (d.element == Element.Light || d.skill == UnitSkill.AlwaysCounter) return Loc.T("guide.short_light");
            return Loc.F("guide.short", Loc.ElementName(ElementRules.Beats(d.element)), Loc.ElementName(ElementRules.WeakTo(d.element)));
        }

        /// <summary>One line for an animal's card: what it beats and what resists it.</summary>
        public static string MatchupLine(UnitDef d, BalanceConfig b)
        {
            if (d.skill == UnitSkill.AlwaysCounter) return Loc.F("guide.always", b.elementBonusPercent);
            if (d.element == Element.Light) return Loc.F("guide.matchup_light", b.lightVsBigPercent);
            return Loc.F("guide.matchup", Loc.ElementName(ElementRules.Beats(d.element)), Loc.ElementName(ElementRules.WeakTo(d.element)));
        }
    }

    /// <summary>Tiny "this wave -> next wave" element chip: big dot, arrow, small dot.</summary>
    public static class WaveChip
    {
        public static void Draw(RectTransform holder, Element now, Element next)
        {
            UIKit.Clear(holder);
            var h = holder.rect.height > 1f ? holder.rect.height : 60f;
            Dot(holder, now, new Vector2(0f, 0.5f), new Vector2(h * 0.5f, 0f), h * 0.9f);
            var arrow = UIKit.Label(holder, "→", (int)(h * 0.55f), Art.Hex(0x3B2412));
            var shadow = arrow.GetComponent<Shadow>();
            if (shadow != null) shadow.enabled = false;
            arrow.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(h * 1.18f, 0f), new Vector2(h * 0.6f, h));
            Dot(holder, next, new Vector2(0f, 0.5f), new Vector2(h * 1.68f, 0f), h * 0.6f);
        }

        static void Dot(RectTransform holder, Element e, Vector2 anchor, Vector2 pos, float size)
        {
            var rim = UIKit.Icon(holder, "dot", Art.Circle, Art.Hex(0x3B2412)).rectTransform
                .Place(anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            UIKit.Icon(rim, "fill", Art.Circle, e == Element.None ? Art.Hex(0x9C8AC4) : Art.ElementColor(e)).rectTransform.Stretch(size * 0.1f, size * 0.1f, size * 0.1f, size * 0.1f);
            var emblem = ElementGuide.Emblem(e);
            if (emblem != null) UIKit.Icon(rim, "emblem", emblem, Color.white).rectTransform.Stretch(size * 0.12f, size * 0.12f, size * 0.12f, size * 0.12f);
        }
    }
}
