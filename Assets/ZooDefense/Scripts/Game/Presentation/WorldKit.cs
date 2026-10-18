using System.Collections.Generic;
using UnityEngine;

namespace ZooDefense.Game
{
    /// <summary>Sorting orders for world sprites (single sorting layer).</summary>
    public static class Order
    {
        public const int Background = -100;
        public const int BoardBase = 0;
        public const int Tiles = 1;
        public const int Path = 2;
        public const int CellHighlight = 3;
        public const int Units = 10;
        public const int Enemies = 20;
        public const int Bars = 30;
        public const int Fx = 40;
        public const int Lock = 45;
        public const int Drag = 60;
        public const int FloatingText = 70;
    }

    public static class WorldKit
    {
        public static GameObject Node(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go;
        }

        public static SpriteRenderer Sprite(string name, Transform parent, Sprite sprite, Color color, int order, Vector3 localPos, float scale = 1f)
        {
            var go = Node(name, parent, localPos);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>Rounded rectangle of an exact world size (9-sliced).</summary>
        public static SpriteRenderer Box(string name, Transform parent, Vector2 size, Color color, int order, Vector3 localPos)
        {
            var sr = Sprite(name, parent, Art.Rounded, color, order, localPos);
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            return sr;
        }

        public static TextMesh Text(string name, Transform parent, string text, float height, Color color, int order, Vector3 localPos,
            TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = Node(name, parent, localPos);
            var tm = go.AddComponent<TextMesh>();
            tm.font = Art.Font;
            tm.fontSize = 64;
            tm.fontStyle = Art.Bold;
            tm.characterSize = height / 6.4f;   // ~line height in world units
            tm.anchor = anchor;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.text = text;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Art.Font.material;
            mr.sortingOrder = order;
            return tm;
        }

        /// <summary>Fits a sprite of any import size into a square of the given world size.</summary>
        public static void FitSprite(SpriteRenderer sr, float worldSize)
        {
            if (sr.sprite == null) return;
            var b = sr.sprite.bounds.size;
            float max = Mathf.Max(b.x, b.y);
            float s = max > 0.0001f ? worldSize / max : 1f;
            sr.transform.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>Tiny pooled effects: projectiles, hit puffs and floating numbers.</summary>
    public sealed class FxPool : MonoBehaviour
    {
        sealed class Fx
        {
            public GameObject go;
            public SpriteRenderer sr;
            public TextMesh tm;
            public Vector3 from, to;
            public float t, duration, startScale, endScale;
            public Color color;
            public bool active;
            public int kind; // 0 projectile, 1 puff, 2 text, 3 sprite effect
            // sprite-effect extras
            public float delay, fadeIn, fadeOut;
            public bool fall;
            public float burst;
            // projectile extras
            public float arc, spin, trailTimer, impactSize, norm = 1f;
            public bool aim;
            public Color trail, impact;
            public Sprite defaultSprite;
        }

        readonly List<Fx> _all = new List<Fx>();

        public void Projectile(Vector3 from, Vector3 to, Color color, float size)
        {
            var fx = Get(0);
            fx.from = from; fx.to = to; fx.duration = 0.14f; fx.color = color;
            fx.startScale = size; fx.endScale = size * 0.8f;
            fx.arc = 0f; fx.spin = 0f; fx.aim = false; fx.impactSize = 0f; fx.trail.a = 0f;
            fx.sr.sprite = fx.defaultSprite;
            fx.norm = 1f;
        }

        /// <summary>
        /// Attack shot of a defender ("chưởng"). Uses ZooArt/Projectiles art when present, otherwise an
        /// element-styled shape: fire orb, water drop (lobbed), spinning leaf, light star. Ends with an impact puff.
        /// </summary>
        public void Shot(Vector3 from, Vector3 to, ZooDefense.Core.UnitDef def, float worldScale)
        {
            var fx = Get(0);
            var color = Art.ElementColor(def.element);
            var art = Art.Projectile(def.id, def.element);
            float dist = Vector3.Distance(from, to);
            fx.from = from; fx.to = to; fx.color = art != null ? Color.white : color;
            // Slow enough to follow with the eye (the sim already applied the damage).
            fx.duration = Mathf.Clamp(dist / (4.5f * Mathf.Max(0.2f, worldScale)), 0.2f, 0.5f);
            fx.aim = true; fx.spin = 0f; fx.arc = 0f;
            fx.trail = Color.Lerp(color, Color.white, 0.35f);
            fx.impact = Color.Lerp(color, Color.white, 0.25f);
            fx.impactSize = (def.splashRadius > 0 ? 1.1f : 0.55f) * worldScale;
            float size = 0.28f * worldScale;
            bool area = def.role == ZooDefense.Core.Role.Area;
            // Species art (square, padded) reads best a bit bigger than the generic element art.
            if (art != null) { fx.sr.sprite = art; size = (Art.SpeciesProjectile(def.id) != null ? 0.62f : 0.45f) * worldScale; }
            else
            {
                switch (def.element)
                {
                    case ZooDefense.Core.Element.Fire: fx.sr.sprite = Art.Circle; size = 0.3f * worldScale; break;
                    case ZooDefense.Core.Element.Water: fx.sr.sprite = Art.Circle; fx.arc = 0.35f * dist; fx.aim = false; break;
                    case ZooDefense.Core.Element.Grass: fx.sr.sprite = Art.Diamond; fx.spin = 900f; fx.aim = false; break;
                    case ZooDefense.Core.Element.Light: fx.sr.sprite = Art.Diamond; fx.spin = 540f; fx.aim = false; size = 0.32f * worldScale; break;
                    default: fx.sr.sprite = Art.Circle; break;
                }
            }
            if (area) { size *= 1.3f; fx.arc = Mathf.Max(fx.arc, 0.3f * dist); }
            fx.startScale = size; fx.endScale = size * 0.85f;
            fx.trailTimer = 0f;
            // Art of any import size is scaled so its longest side equals `size` world units.
            var b = fx.sr.sprite != null ? fx.sr.sprite.bounds.size : Vector3.one;
            float max = Mathf.Max(b.x, b.y);
            fx.norm = max > 0.0001f ? 1f / max : 1f;
            if (art != null) fx.trail.a = 0f;   // the art carries its own tail
        }

        /// <summary>
        /// Sprite effect: a sprite (any import size, fitted to `size` world units) travelling from -> to while it
        /// scales size0 -> size1, spins, fades in and out. fall = accelerating (things dropping from the sky).
        /// burst > 0 leaves a ring puff of that size where it ends. delay postpones the start.
        /// </summary>
        public void SpriteFx(Sprite sprite, Vector3 from, Vector3 to, float size0, float size1, float duration, Color color,
            float spin = 0f, float delay = 0f, bool fall = false, float burst = 0f, float fadeIn = 0.15f, float fadeOut = 0.35f)
        {
            if (sprite == null) return;
            var fx = Get(3);
            fx.sr.sprite = sprite;
            fx.from = from; fx.to = to; fx.duration = Mathf.Max(0.05f, duration); fx.color = color;
            fx.startScale = size0; fx.endScale = size1; fx.spin = spin; fx.delay = delay; fx.fall = fall; fx.burst = burst;
            fx.fadeIn = fadeIn; fx.fadeOut = fadeOut;
            fx.t = -delay;
            var b = sprite.bounds.size;
            float max = Mathf.Max(b.x, b.y);
            fx.norm = max > 0.0001f ? 1f / max : 1f;
            fx.go.transform.position = from;
            fx.go.transform.localScale = Vector3.zero;
            fx.go.transform.rotation = Quaternion.identity;
        }

        public void Puff(Vector3 at, Color color, float size, float duration = 0.3f)
        {
            var fx = Get(1);
            fx.from = at; fx.to = at; fx.duration = duration; fx.color = color;
            fx.startScale = size * 0.3f; fx.endScale = size;
        }

        public void FloatText(Vector3 at, string text, Color color, float height = 0.35f)
        {
            var fx = Get(2);
            fx.from = at; fx.to = at + new Vector3(0, 0.7f, 0); fx.duration = 0.8f; fx.color = color;
            fx.tm.text = text;
            fx.tm.characterSize = height / 6.4f;
            fx.startScale = fx.endScale = 1f;
        }

        Fx Get(int kind)
        {
            for (int i = 0; i < _all.Count; i++)
                if (!_all[i].active && _all[i].kind == kind) { _all[i].active = true; _all[i].t = 0; _all[i].go.SetActive(true); return _all[i]; }

            var fx = new Fx { kind = kind, active = true };
            if (kind == 2)
            {
                fx.tm = WorldKit.Text("fx_text", transform, "", 0.35f, Color.white, Order.FloatingText, Vector3.zero);
                fx.go = fx.tm.gameObject;
            }
            else
            {
                fx.sr = WorldKit.Sprite(kind == 0 ? "fx_shot" : (kind == 3 ? "fx_sprite" : "fx_puff"), transform, kind == 0 ? Art.Circle : Art.Ring, Color.white,
                    kind == 3 ? Order.Fx + 3 : Order.Fx, Vector3.zero);
                fx.go = fx.sr.gameObject;
                fx.defaultSprite = fx.sr.sprite;
            }
            _all.Add(fx);
            return fx;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _all.Count; i++)
            {
                var fx = _all[i];
                if (!fx.active) continue;
                fx.t += dt;
                if (fx.kind == 3)
                {
                    if (fx.t < 0f) { fx.go.transform.localScale = Vector3.zero; continue; }
                    float q = Mathf.Clamp01(fx.t / fx.duration);
                    float e = fx.fall ? q * q : 1f - (1f - q) * (1f - q);
                    fx.go.transform.position = Vector3.Lerp(fx.from, fx.to, e);
                    fx.go.transform.rotation = Quaternion.Euler(0f, 0f, fx.spin * fx.t);
                    float sc = Mathf.Lerp(fx.startScale, fx.endScale, 1f - (1f - q) * (1f - q)) * fx.norm;
                    fx.go.transform.localScale = new Vector3(sc, sc, 1f);
                    float a = 1f;
                    if (fx.fadeIn > 0f && q < fx.fadeIn) a = q / fx.fadeIn;
                    if (fx.fadeOut > 0f && q > 1f - fx.fadeOut) a = Mathf.Min(a, (1f - q) / fx.fadeOut);
                    var col = fx.color; col.a *= a;
                    fx.sr.color = col;
                    if (q >= 1f)
                    {
                        fx.active = false;
                        fx.go.SetActive(false);
                        if (fx.burst > 0f) Puff(fx.to, fx.color, fx.burst, 0.35f);
                    }
                    continue;
                }
                float k = Mathf.Clamp01(fx.t / fx.duration);
                var pos = Vector3.Lerp(fx.from, fx.to, fx.kind == 2 ? 1f - (1f - k) * (1f - k) : k);
                if (fx.kind == 0)
                {
                    pos.y += fx.arc * 4f * k * (1f - k);   // parabola peaking mid-flight
                    if (fx.aim)
                    {
                        var dir = fx.to - fx.from;
                        dir.y += fx.arc * 4f * (1f - 2f * k);
                        fx.go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                    }
                    else fx.go.transform.rotation = Quaternion.Euler(0f, 0f, fx.spin * fx.t);
                    // Little fading trail dots behind the shot.
                    fx.trailTimer -= dt;
                    if (fx.trail.a > 0f && fx.trailTimer <= 0f && k < 1f)
                    {
                        fx.trailTimer = 0.03f;
                        Puff(pos, fx.trail, fx.startScale * 1.4f, 0.16f);
                    }
                }
                fx.go.transform.position = pos;
                float s = Mathf.Lerp(fx.startScale, fx.endScale, k);
                var c = fx.color;
                if (fx.kind != 0) c.a *= 1f - k;
                if (fx.sr != null) { fx.sr.color = c; s *= fx.kind == 0 ? fx.norm : 1f; fx.go.transform.localScale = new Vector3(s, s, 1f); }
                if (fx.tm != null) fx.tm.color = c;
                if (k >= 1f)
                {
                    fx.active = false;
                    fx.go.SetActive(false);
                    if (fx.kind == 0 && fx.impactSize > 0f) Puff(fx.to, fx.impact, fx.impactSize, 0.22f);
                }
            }
        }
    }
}
