using System;
using UnityEngine;
using UnityEngine.UI;

namespace ZooDefense.Game
{
    /// <summary>Palette for the placeholder UI. Swap these (or replace with sprites) when the real UI kit arrives.</summary>
    public static class Palette
    {
        public static readonly Color Background = Art.Hex(0x2B2140);
        public static readonly Color Panel = Art.Hex(0x3A2D57);
        public static readonly Color PanelDark = Art.Hex(0x221A33);
        public static readonly Color Text = Color.white;
        public static readonly Color TextDim = Art.Hex(0xC9BEE6);
        public static readonly Color Primary = Art.Hex(0xFFB020);
        public static readonly Color Green = Art.Hex(0x4CC46B);
        public static readonly Color Blue = Art.Hex(0x3D8BFF);
        public static readonly Color Red = Art.Hex(0xE5484D);
        public static readonly Color Purple = Art.Hex(0x9B5CFF);
        public static readonly Color Candy = Art.Hex(0xFF7AC8);
        public static readonly Color Disabled = Art.Hex(0x5E5670);
    }

    /// <summary>Builds uGUI hierarchies from code (no prefabs needed for the prototype).</summary>
    public static class UIKit
    {
        public const int UILayer = 5;

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UILayer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Anchor to a point of the parent (0..1), place by pivot, fixed size in reference pixels.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Stretch horizontally, fixed height, anchored at the top (0) or bottom (1) edge.</summary>
        public static RectTransform Band(this RectTransform rt, bool top, float height, float offset = 0, float sideMargin = 0)
        {
            rt.anchorMin = new Vector2(0, top ? 1 : 0);
            rt.anchorMax = new Vector2(1, top ? 1 : 0);
            rt.pivot = new Vector2(0.5f, top ? 1 : 0);
            rt.anchoredPosition = new Vector2(0, top ? -offset : offset);
            rt.sizeDelta = new Vector2(-2 * sideMargin, height);
            return rt;
        }

        public static Image Panel(Transform parent, string name, Color color, bool rounded = true)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (rounded) { img.sprite = Art.Rounded; img.type = Image.Type.Sliced; }
            img.raycastTarget = false;
            return img;
        }

        public static Image Icon(Transform parent, string name, Sprite sprite, Color color)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            var rt = Rect("label", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Art.Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style == FontStyle.Bold ? Art.Bold : style;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = rt.gameObject.AddComponent<Shadow>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.45f);
            outline.effectDistance = new Vector2(0f, -3f);
            return t;
        }

        public static Button Button(Transform parent, string name, string text, Color color, Action onClick, int fontSize = 44)
        {
            var img = Panel(parent, name, color);
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);
            btn.colors = colors;
            btn.onClick.AddListener(() => AudioService.Play(Sfx.Click));
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            // Always add the label (possibly empty) so SetButtonText works on every button.
            var label = Label(img.transform, text ?? "", fontSize, Palette.Text);
            label.rectTransform.Stretch(8, 8, 4, 4);
            return btn;
        }

        /// <summary>
        /// Game-style "3D" button: a darker base under a raised face with a top shine. The face dips when pressed.
        /// Optional art ZooArt/UI/{artName}.png replaces the drawn face (9-sliced if it has borders).
        /// </summary>
        public static Button ChunkyButton(Transform parent, string name, string text, Color color, Action onClick, int fontSize = 44, string artName = null)
        {
            var root = Rect(name, parent);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            var shade = Panel(root, "shade", Color.Lerp(color, Color.black, 0.45f));
            shade.rectTransform.Stretch(0, 0, 12, 0);
            var face = Panel(root, "face", color);
            var art = artName != null ? Art.Icon(artName) : null;
            if (art != null)
            {
                face.sprite = art;
                face.type = art.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
                face.color = Color.white;
                shade.gameObject.SetActive(false);
            }
            face.rectTransform.Stretch(0, 0, 0, 12);
            var shine = Panel(face.transform, "shine", new Color(1f, 1f, 1f, 0.22f));
            shine.rectTransform.Stretch(10, 10, 6, 0);
            shine.rectTransform.anchorMin = new Vector2(0f, 0.55f);
            shine.gameObject.SetActive(art == null);
            var label = Label(face.transform, text ?? "", fontSize, Palette.Text);
            label.rectTransform.Stretch(10, 10, 4, 4);
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
            outline.effectDistance = new Vector2(2f, -2f);

            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = face;
            var colors = btn.colors;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
            btn.colors = colors;
            root.gameObject.AddComponent<PressDip>().face = face.rectTransform;
            btn.onClick.AddListener(() => AudioService.Play(Sfx.Click));
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        /// <summary>Adds (or returns) the small motion helper on a UI element.</summary>
        public static UiMotion Motion(RectTransform rt)
        {
            var m = rt.GetComponent<UiMotion>();
            return m != null ? m : rt.gameObject.AddComponent<UiMotion>();
        }

        /// <summary>Thick dark outline for white text on bright art.</summary>
        public static Text Outlined(Text t, float size = 3f)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.2f, 0.1f, 0.05f, 0.85f);
            o.effectDistance = new Vector2(size, -size);
            return t;
        }

        /// <summary>Moves the face of a ChunkyButton down while it is held.</summary>
        sealed class PressDip : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler
        {
            public RectTransform face;
            public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) { if (face != null) { face.offsetMin = new Vector2(0f, 4f); face.offsetMax = new Vector2(0f, -8f); } }
            public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) { if (face != null) { face.offsetMin = new Vector2(0f, 12f); face.offsetMax = Vector2.zero; } }
        }

        /// <summary>Fills the parent like CSS background-size: cover (keeps the image's proportions, crops the overflow).</summary>
        public static void Cover(Image img)
        {
            img.preserveAspect = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            var fitter = img.gameObject.AddComponent<AspectRatioFitter>();
            var r = img.sprite != null ? img.sprite.rect : new Rect(0, 0, 9, 16);
            fitter.aspectRatio = r.height > 0 ? r.width / r.height : 0.5625f;
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        }

        public static void SetButtonText(Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }
    }

    /// <summary>
    /// Tiny self-running UI animations (no screen Tick needed): pop-in with overshoot, pulse, spin, bob.
    /// Uses unscaled time so it also runs while a match is paused.
    /// </summary>
    public sealed class UiMotion : MonoBehaviour
    {
        public float popDelay = -1f, popTime = 0.3f;
        public float pulseAmp, pulseSpeed = 4f;
        public float spin;
        public float bobAmp, bobSpeed = 3f;
        float _t;
        Vector2 _base;
        bool _baseSet;

        public UiMotion PopIn(float delay = 0f, float time = 0.3f) { popDelay = delay; popTime = time; transform.localScale = Vector3.zero; return this; }
        public UiMotion Pulse(float amp, float speed = 4f) { pulseAmp = amp; pulseSpeed = speed; return this; }
        public UiMotion Spin(float degPerSec) { spin = degPerSec; return this; }
        public UiMotion Bob(float amp, float speed = 3f) { bobAmp = amp; bobSpeed = speed; return this; }

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            var rt = (RectTransform)transform;
            float s = 1f;
            if (popDelay >= 0f)
            {
                float k = (_t - popDelay) / Mathf.Max(0.01f, popTime);
                s = k <= 0f ? 0f : Deco.EaseOutBack(k);
                if (k >= 1f) popDelay = -1f;
            }
            if (pulseAmp > 0f) s *= 1f + pulseAmp * Mathf.Sin(_t * pulseSpeed);
            rt.localScale = new Vector3(s, s, 1f);
            if (spin != 0f) rt.localRotation = Quaternion.Euler(0f, 0f, -_t * spin);
            if (bobAmp <= 0f && _baseSet) { rt.anchoredPosition = _base; _baseSet = false; }
            if (bobAmp > 0f)
            {
                if (!_baseSet) { _base = rt.anchoredPosition; _baseSet = true; }
                rt.anchoredPosition = _base + new Vector2(0f, Mathf.Abs(Mathf.Sin(_t * bobSpeed)) * bobAmp);
            }
        }
    }

    /// <summary>Cycles an Image's colour through the rainbow (SSS badges and frames).</summary>
    public sealed class RainbowTint : MonoBehaviour
    {
        Image _img;
        float _t;
        void Awake() { _img = GetComponent<Image>(); }
        void Update()
        {
            if (_img == null) return;
            _t += Time.unscaledDeltaTime;
            var c = Color.HSVToRGB(Mathf.Repeat(_t * 0.25f, 1f), 0.6f, 1f);
            c.a = _img.color.a;
            _img.color = c;
        }
    }
}
