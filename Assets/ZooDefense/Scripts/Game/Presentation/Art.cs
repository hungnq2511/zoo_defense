using System.Collections.Generic;
using UnityEngine;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>
    /// Looks up real art by naming convention and falls back to generated placeholder shapes,
    /// so the game is fully playable before any asset exists.
    ///
    /// Drop PNGs (Texture Type: Sprite - the importer in Editor/ does this automatically) into:
    ///   Assets/ZooDefense/Resources/ZooArt/Units/{unitId}_1.png   level 1-3 look
    ///                                       /{unitId}_4.png   level 4-6 look (optional)
    ///                                       /{unitId}_7.png   level 7 look (optional)
    ///                                       /{unitId}.png     single look for all levels
    ///   Assets/ZooDefense/Resources/ZooArt/Enemies/{enemyId}.png
    ///   Assets/ZooDefense/Resources/ZooArt/EnemyWalks/{enemyId}.png   4x4 walk sheet + catalog.json (optional)
    ///   Assets/ZooDefense/Resources/ZooArt/Projectiles/{unitId|fire|water|grass|light}.png   (optional)
    ///   Assets/ZooDefense/Resources/ZooArt/Backgrounds/arena_{1..5}.png
    ///   Assets/ZooDefense/Resources/ZooArt/UI/{candy|heart|points|trophy|gold}.png
    /// Ids are listed in ART_NAMING.md and via the menu Zoo Defense > Check Art.
    /// </summary>
    public static class Art
    {
        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        static Sprite Load(string path)
        {
            Sprite s;
            if (_cache.TryGetValue(path, out s)) return s;
            s = Resources.Load<Sprite>(path);
            _cache[path] = s;
            return s;
        }

        public static void ClearCache() { _cache.Clear(); _walks = null; }

        public static Sprite Unit(string unitId, int level)
        {
            if (level >= 7) { var s7 = Load("ZooArt/Units/" + unitId + "_7"); if (s7 != null) return s7; }
            if (level >= 4) { var s4 = Load("ZooArt/Units/" + unitId + "_4"); if (s4 != null) return s4; }
            var s1 = Load("ZooArt/Units/" + unitId + "_1");
            return s1 != null ? s1 : Load("ZooArt/Units/" + unitId);
        }

        public static Sprite Enemy(string enemyId) { return Load("ZooArt/Enemies/" + enemyId); }

        // ------------------------------------------------------------------ enemy walk sheets

        /// <summary>One enemy walk cycle: a grid sheet in Resources/ZooArt/EnemyWalks/{enemyId}.png.</summary>
        [System.Serializable]
        public sealed class WalkClip
        {
            public string id, kind;
            public int columns = 4, rows = 4, frameCount = 16;
            public float fps = 12f, pivotX = 0.5f, pivotY = 0.12f;
            /// <summary>Size multiplier so the walking art matches the size of the still art on the board.</summary>
            public float fit = 1f;
            [System.NonSerialized] public Sprite[] frames;
        }

        [System.Serializable] sealed class WalkCatalog { public WalkClip[] clips; }

        static Dictionary<string, WalkClip> _walks;

        /// <summary>
        /// Walk cycle for an enemy, or null (then the still art + procedural bob is used).
        /// Frames are cut at runtime; each frame is exactly 1 world unit square whatever the import size.
        /// </summary>
        public static WalkClip EnemyWalk(string enemyId)
        {
            if (_walks == null)
            {
                _walks = new Dictionary<string, WalkClip>();
                var json = Resources.Load<TextAsset>("ZooArt/EnemyWalks/catalog");
                if (json != null)
                {
                    try
                    {
                        var cat = JsonUtility.FromJson<WalkCatalog>(json.text);
                        if (cat != null && cat.clips != null) foreach (var c in cat.clips) if (c != null && c.id != null) _walks[c.id] = c;
                    }
                    catch (System.Exception e) { Debug.LogWarning("[ZooDefense] EnemyWalks/catalog.json unreadable: " + e.Message); }
                }
            }
            WalkClip clip;
            if (enemyId == null || !_walks.TryGetValue(enemyId, out clip)) return null;
            if (clip.frames == null)
            {
                var tex = Resources.Load<Texture2D>("ZooArt/EnemyWalks/" + enemyId);
                if (tex == null) { _walks.Remove(enemyId); return null; }
                float fw = tex.width / (float)clip.columns, fh = tex.height / (float)clip.rows;
                clip.frames = new Sprite[clip.frameCount];
                for (int i = 0; i < clip.frameCount; i++)
                {
                    int col = i % clip.columns, row = i / clip.columns;
                    var rect = new Rect(col * fw, tex.height - (row + 1) * fh, fw, fh);
                    clip.frames[i] = Sprite.Create(tex, rect, new Vector2(clip.pivotX, clip.pivotY), fw, 0, SpriteMeshType.FullRect, Vector4.zero);
                    clip.frames[i].name = enemyId + "_walk_" + i;
                }
            }
            return clip;
        }

        /// <summary>Optional projectile art: ZooArt/Projectiles/{unitId}.png, else {element}.png (pointing right).</summary>
        /// <summary>Species-specific projectile art (ZooArt/Projectiles/{unitId}.png) or null.</summary>
        public static Sprite SpeciesProjectile(string unitId) { return Load("ZooArt/Projectiles/" + unitId); }
        public static Sprite Projectile(string unitId, Element element)
        {
            var s = Load("ZooArt/Projectiles/" + unitId);
            return s != null ? s : Load("ZooArt/Projectiles/" + element.ToString().ToLowerInvariant());
        }
        public static Sprite Background(int arenaIndex) { return Load("ZooArt/Backgrounds/arena_" + arenaIndex); }
        /// <summary>Loading / matchmaking artwork (portrait, sky at the top left empty for the logo).</summary>
        public static Sprite Loading() { return Load("ZooArt/Backgrounds/loading"); }
        public static Sprite Icon(string name) { return Load("ZooArt/UI/" + name); }

        // ------------------------------------------------------------------ colours

        public static Color ElementColor(Element e)
        {
            switch (e)
            {
                case Element.Fire: return Hex(0xFF6B3D);
                case Element.Water: return Hex(0x3DB8FF);
                case Element.Grass: return Hex(0x6BCB3D);
                case Element.Light: return Hex(0xFFD166);
                default: return Hex(0x9C8AC4);
            }
        }

        public static Color RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return Hex(0x4D9DFF);
                case Rarity.Epic: return Hex(0xB46BFF);
                case Rarity.Legendary: return Hex(0xFFB020);
                default: return Hex(0xB9C3CF);
            }
        }

        public static string ElementName(Element e)
        {
            switch (e) { case Element.Fire: return "Fire"; case Element.Water: return "Water"; case Element.Grass: return "Grass"; case Element.Light: return "Light"; default: return "-"; }
        }

        public static Color Hex(int rgb, float a = 1f)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);
        }

        public static string Initials(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return "?";
            var parts = displayName.Split(' ');
            if (parts.Length >= 2) return (parts[0].Substring(0, 1) + parts[1].Substring(0, 1)).ToUpperInvariant();
            return displayName.Substring(0, Mathf.Min(2, displayName.Length)).ToUpperInvariant();
        }

        // ------------------------------------------------------------------ generated shapes

        static Sprite _circle, _ring, _rounded, _square, _diamond;

        /// <summary>Soft-edged white disc, 1 world unit wide at scale 1.</summary>
        public static Sprite Circle { get { return _circle != null ? _circle : (_circle = MakeCircle(128, false)); } }
        /// <summary>Ring outline, 1 world unit wide.</summary>
        public static Sprite Ring { get { return _ring != null ? _ring : (_ring = MakeCircle(128, true)); } }
        /// <summary>9-sliced rounded rectangle, use with SpriteDrawMode.Sliced or Image.Type.Sliced.</summary>
        public static Sprite Rounded { get { return _rounded != null ? _rounded : (_rounded = MakeRounded(64, 22)); } }
        public static Sprite Square { get { return _square != null ? _square : (_square = MakeRounded(8, 0)); } }
        public static Sprite Diamond { get { return _diamond != null ? _diamond : (_diamond = MakeDiamond(64)); } }

        static Sprite _gradient, _rays, _glow;
        /// <summary>Vertical fade: opaque at the bottom, transparent at the top (flip with scale y = -1).</summary>
        public static Sprite GradientV { get { return _gradient != null ? _gradient : (_gradient = MakeGradient(64)); } }
        /// <summary>Sun-burst rays for victory / reward backdrops. Optional art: ZooArt/UI/rays.png.</summary>
        public static Sprite Rays { get { var a = Icon("rays"); return a != null ? a : (_rays != null ? _rays : (_rays = MakeRays(256, 16))); } }
        /// <summary>Soft radial glow (bright centre fading out).</summary>
        public static Sprite Glow { get { return _glow != null ? _glow : (_glow = MakeGlow(128)); } }

        static Sprite MakeGradient(int h)
        {
            var t = NewTexture(4, h);
            var px = new Color32[4 * h];
            for (int y = 0; y < h; y++)
            {
                float a = 1f - (float)y / (h - 1);
                a = a * a * (3f - 2f * a);
                for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            t.SetPixels32(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, 4, h), new Vector2(0.5f, 0.5f), h);
        }

        static Sprite MakeRays(int size, int count)
        {
            var t = NewTexture(size, size);
            var px = new Color32[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / c;
                    float ang = Mathf.Atan2(dy, dx) / (2f * Mathf.PI) * count;
                    float wedge = Mathf.Abs((ang - Mathf.Floor(ang)) - 0.5f) * 2f;
                    float a = Mathf.Clamp01((0.55f - wedge) * 6f) * Mathf.Clamp01(1f - d) * Mathf.Clamp01(d * 4f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite MakeGlow(int size)
        {
            var t = NewTexture(size, size);
            var px = new Color32[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c)) / c;
                    float a = Mathf.Clamp01(1f - d);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
                }
            t.SetPixels32(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Texture2D NewTexture(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.hideFlags = HideFlags.DontSave;
            return t;
        }

        static Sprite MakeCircle(int size, bool ring)
        {
            var t = NewTexture(size, size);
            var px = new Color32[size * size];
            float r = size * 0.5f - 1f, c = size * 0.5f - 0.5f, thickness = size * 0.08f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    if (ring) a *= Mathf.Clamp01(d - (r - thickness) + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite MakeRounded(int size, int radius)
        {
            var t = NewTexture(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float a = 1f;
                    if (radius > 0)
                    {
                        float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                        float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                        float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                        a = Mathf.Clamp01(radius - d + 0.5f);
                    }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px);
            t.Apply();
            float b = Mathf.Max(radius, 1);
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        static Sprite MakeDiamond(int size)
        {
            var t = NewTexture(size, size);
            var px = new Color32[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Abs(x + 0.5f - c) + Mathf.Abs(y + 0.5f - c);
                    float a = Mathf.Clamp01(c - 1 - d + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Font _font;
        static bool _custom;
        public static Font Font
        {
            get
            {
                // Optional project font (must include Vietnamese glyphs): Resources/ZooArt/Fonts/Main.ttf
                if (_font == null) { _font = Resources.Load<Font>("ZooArt/Fonts/Main"); _custom = _font != null; }
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        /// <summary>
        /// Style to request for "bold" text. The project font (Baloo 2 Bold) is already bold, so asking Unity for
        /// Bold again would add a blurry synthetic bold on top.
        /// </summary>
        public static FontStyle Bold { get { var f = Font; return _custom ? FontStyle.Normal : FontStyle.Bold; } }
    }
}
