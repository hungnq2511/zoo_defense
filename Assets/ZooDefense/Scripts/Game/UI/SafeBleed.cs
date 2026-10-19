using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZooDefense.Game
{
    /// <summary>
    /// Full-screen look on notched phones. Buttons and text stay inside the safe area (screen minus notch / home bar),
    /// but backgrounds "bleed" to the real screen edges:
    ///   - full-screen backgrounds (panels, art) are stretched over the notch and home-bar strips,
    ///   - art that covers the screen keeps its aspect and covers the whole screen,
    ///   - translucent overlays and the header tint reach the screen edges,
    /// Applied automatically to every screen and dialog by GameApp (no per-screen code).
    /// </summary>
    public static class SafeBleed
    {
        const string Prefix = "~bleed";

        /// <summary>Safe-area insets in UI units: x = left, y = bottom, z = right, w = top.</summary>
        public static Vector4 Insets;

        sealed class Marked : MonoBehaviour { }

        public static void Apply(RectTransform root)
        {
            if (root == null) return;
            var children = new List<RectTransform>();
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i) as RectTransform;
                if (c == null) continue;
                if (c.name.StartsWith(Prefix))
                {
                    if (c.name == Prefix + "box") Outset(c);
                    else { Object.DestroyImmediate(c.gameObject); i--; }   // fillers are rebuilt below
                    continue;
                }
                children.Add(c);
            }
            foreach (var c in children) ApplyOne(root, c);
        }

        static void ApplyOne(RectTransform root, RectTransform c)
        {
            var fitter = c.GetComponent<AspectRatioFitter>();
            if (fitter != null && fitter.aspectMode == AspectRatioFitter.AspectMode.EnvelopeParent)
            {
                // Cover art: give it a full-screen parent so it envelopes the whole screen.
                var box = UIKit.Rect(Prefix + "box", root);
                box.SetSiblingIndex(c.GetSiblingIndex());
                Outset(box);
                c.SetParent(box, false);
                return;
            }

            var g = c.GetComponent<Graphic>();
            if (g == null) return;
            bool fullStretch = c.anchorMin == Vector2.zero && c.anchorMax == Vector2.one;
            if (fullStretch)
            {
                bool flush = c.GetComponent<Marked>() != null || (c.offsetMin.sqrMagnitude < 1f && c.offsetMax.sqrMagnitude < 1f);
                if (!flush) return;
                if (OnlyStretchedChildren(c))
                {
                    if (c.GetComponent<Marked>() == null) c.gameObject.AddComponent<Marked>();
                    Outset(c);
                }
                else
                {
                    // A full-screen panel holding laid-out content (dialog dim, pause panel): keep it, paint a copy behind.
                    var fill = Filler(root, c, g, true);
                    if (fill != null) Outset(fill.rectTransform);
                }
                return;
            }

            // Full-width strips flush with the top / bottom of the safe area:
            //   - plain overlays (dark fades, tints) stretch to the screen edge, so the shading covers the notch
            //     and the home-indicator strip too,
            //   - the header bar (translucent, holds the currencies) gets a copy of its tint above it, under the notch.
            // Opaque coloured bars are left alone (the tab bar reaches the bottom by itself).
            bool fullWidth = c.anchorMin.x <= 0.001f && c.anchorMax.x >= 0.999f && Mathf.Abs(c.offsetMin.x) <= 24f && Mathf.Abs(c.offsetMax.x) <= 24f;
            if (!fullWidth) return;
            var orig = c.GetComponent<Original>();
            Vector2 min = orig != null ? orig.min : c.offsetMin, max = orig != null ? orig.max : c.offsetMax;
            bool top = c.anchorMax.y >= 0.999f && Mathf.Abs(max.y) < 2f;
            bool bottom = c.anchorMin.y <= 0.001f && Mathf.Abs(min.y) < 2f;
            if (!top && !bottom) return;
            bool translucent = g.color.a < 0.99f;
            if (c.childCount == 0 && translucent)
            {
                if (orig == null) { orig = c.gameObject.AddComponent<Original>(); orig.min = min; orig.max = max; }
                c.offsetMin = new Vector2(min.x, bottom ? min.y - Insets.y : min.y);
                c.offsetMax = new Vector2(max.x, top ? max.y + Insets.w : max.y);
                return;
            }
            if (top && translucent && Insets.w > 0.5f)
            {
                var f = Filler(root, c, g, false);
                if (f != null)
                {
                    var rt = f.rectTransform;
                    rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
                    rt.offsetMin = new Vector2(-Insets.x, 0f);
                    rt.offsetMax = new Vector2(Insets.z, Insets.w);
                }
            }
        }

        /// <summary>Offsets before bleeding (so a safe-area change re-applies cleanly).</summary>
        sealed class Original : MonoBehaviour { public Vector2 min, max; }

        static void Outset(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-Insets.x, -Insets.y);
            rt.offsetMax = new Vector2(Insets.z, Insets.w);
        }

        static bool OnlyStretchedChildren(RectTransform c)
        {
            for (int i = 0; i < c.childCount; i++)
            {
                var k = c.GetChild(i) as RectTransform;
                if (k == null) continue;
                if (k.anchorMin != Vector2.zero || k.anchorMax != Vector2.one) return false;
            }
            return true;
        }

        /// <summary>A plain coloured copy of `g`, drawn just behind `c`. Only for flat colour / panel / gradient graphics.</summary>
        static Image Filler(RectTransform root, RectTransform c, Graphic g, bool copySprite)
        {
            var src = g as Image;
            if (src == null) return null;
            bool flat = src.sprite == null || src.sprite == Art.Rounded || src.sprite == Art.GradientV;
            if (!flat && !copySprite) return null;
            var img = UIKit.Panel(root, Prefix + "fill", src.color, false);
            if (copySprite && src.sprite != null && src.sprite != Art.Rounded)
            {
                img.sprite = src.sprite;
                img.type = src.type;
            }
            img.raycastTarget = src.raycastTarget;   // a dialog's dim still blocks taps on the strips
            img.transform.SetSiblingIndex(c.GetSiblingIndex());
            img.enabled = c.gameObject.activeInHierarchy;
            img.gameObject.AddComponent<FollowActive>().target = c.gameObject;
            return img;
        }

        /// <summary>Fillers show and hide with the element they extend (pause panel, popups).</summary>
        sealed class FollowActive : MonoBehaviour
        {
            public GameObject target;
            Image _img;
            void Awake() { _img = GetComponent<Image>(); }
            void LateUpdate()
            {
                if (target == null) { Destroy(gameObject); return; }
                if (_img == null) return;
                _img.enabled = target.activeInHierarchy;
                // Follow fades (dialog dim fading in / out).
                var g = target.GetComponent<Graphic>();
                if (g != null) _img.color = new Color(g.color.r, g.color.g, g.color.b, g.color.a * g.canvasRenderer.GetAlpha());
            }
        }
    }
}
