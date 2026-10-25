using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>
    /// Full-screen reward reveal used for gacha pulls, chests and every other reward list.
    ///   1. (dramatic only) intro: the gacha egg or the chest shakes harder and harder, then a white flash + ring burst
    ///   2. face-down cards are dealt one by one
    ///   3. cards flip one by one; epic / legendary cards wait a beat, get rotating rays and a punch
    ///   4. OK appears. Tapping anywhere before that reveals everything at once.
    /// Optional art: ZooArt/UI/gacha_egg.png (intro), ZooArt/UI/card_back.png (card backs).
    /// </summary>
    public sealed class RewardReveal : MonoBehaviour
    {
        sealed class Card
        {
            public RectTransform root, back, front, rays;
            public int tier;
            public float dealAt, flipAt = -1f;
            public bool shown;
        }

        GameApp _app;
        RectTransform _root, _intro, _ring, _okRt;
        Image _flash;
        Text _hint;
        readonly List<Card> _cards = new List<Card>();
        float _t, _introEnd, _phaseStart;
        int _phase;   // 0 intro, 1 deal, 2 flip, 3 done
        float _nextFlip;
        int _flipIndex;

        const float FlipTime = 0.3f;

        public static void Show(GameApp app, string title, List<Reward> rewards, ICollection<string> newlyUnlocked, bool dramatic, string introChest)
        {
            var dim = UIKit.Panel(app.OverlayLayer, "rewardReveal", new Color(0.03f, 0.02f, 0.07f, 0.9f), false);
            dim.rectTransform.Stretch();
            dim.raycastTarget = true;
            Modal.Register(dim.gameObject);
            var r = dim.gameObject.AddComponent<RewardReveal>();
            r.Init(app, title, rewards, newlyUnlocked, dramatic, introChest);
        }

        void Init(GameApp app, string title, List<Reward> rewards, ICollection<string> newlyUnlocked, bool dramatic, string introChest)
        {
            _app = app;
            _root = (RectTransform)transform;
            var center = new Vector2(0.5f, 0.5f);

            int best = 0;
            var tiers = new int[rewards.Count];
            for (int i = 0; i < rewards.Count; i++) { tiers[i] = Tier(rewards[i]); best = Mathf.Max(best, tiers[i]); }
            var bestColor = TierColor(best);

            var rays = UIKit.Icon(_root, "rays", Art.Rays, new Color(bestColor.r, bestColor.g, bestColor.b, 0.3f)).rectTransform;
            rays.Place(center, center, new Vector2(0, 60), new Vector2(1800, 1800));
            UIKit.Motion(rays).Spin(10f);
            var glow = UIKit.Icon(_root, "glow", Art.Glow, new Color(bestColor.r, bestColor.g, bestColor.b, 0.35f));
            glow.preserveAspect = false;
            glow.rectTransform.Place(center, center, new Vector2(0, 60), new Vector2(1100, 1000));

            Deco.Ribbon(_root, title, Art.Hex(0x8A4FE0), 50).Place(center, center, new Vector2(0, 660), new Vector2(Mathf.Min(860f, 320f + title.Length * 28f), 130));

            // Tap anywhere = reveal everything now.
            var skip = gameObject.AddComponent<Button>();
            skip.transition = Selectable.Transition.None;
            skip.onClick.AddListener(Skip);

            // Intro object
            if (dramatic)
            {
                _intro = UIKit.Rect("intro", _root).Place(center, center, new Vector2(0, 60), new Vector2(420, 420));
                var ig = UIKit.Icon(_intro, "glow", Art.Glow, new Color(bestColor.r, bestColor.g, bestColor.b, 0.8f));
                ig.preserveAspect = false;
                ig.rectTransform.Stretch(-120, -120, -120, -120);
                if (introChest != null) Icons.Chest(_intro, introChest).Stretch(30, 30, 50, 30);
                else Egg(_intro);
                _introEnd = 1.15f;
                _phase = 0;
            }
            else { _phase = 1; _phaseStart = 0f; }

            // Cards (face down)
            int n = Mathf.Min(rewards.Count, 15);
            int cols = n <= 4 ? n : 5;
            int rows = (n + cols - 1) / cols;
            float w = n <= 4 ? 230f : 188f, h = w * 1.4f, gap = 18f;
            var cardsRoot = UIKit.Rect("cards", _root).Place(center, center, new Vector2(0, 40), new Vector2(cols * (w + gap), rows * (h + gap)));
            for (int i = 0; i < n; i++)
            {
                int row = i / cols, col = i % cols;
                int inRow = Mathf.Min(cols, n - row * cols);
                float x = (col - (inRow - 1) * 0.5f) * (w + gap);
                float y = ((rows - 1) * 0.5f - row) * (h + gap);
                _cards.Add(BuildCard(cardsRoot, rewards[i], tiers[i], newlyUnlocked, new Vector2(x, y), new Vector2(w, h), i));
            }

            _hint = UIKit.Label(_root, Loc.T("reward.tap_skip"), 28, Palette.TextDim);
            _hint.rectTransform.Place(center, center, new Vector2(0, -560), new Vector2(800, 50));

            var ok = UIKit.ChunkyButton(_root, "ok", Loc.T("common.ok"), Palette.Green, () => Modal.Close(gameObject), 48);
            UIKit.Outlined(ok.GetComponentInChildren<Text>(), 3f);
            _okRt = ok.GetComponent<RectTransform>();
            _okRt.Place(center, center, new Vector2(0, -640), new Vector2(480, 136));
            _okRt.gameObject.SetActive(false);

            // Burst ring + white flash on top
            _ring = UIKit.Icon(_root, "ring", Art.Ring, new Color(1f, 1f, 1f, 0f)).rectTransform;
            _ring.Place(center, center, new Vector2(0, 60), new Vector2(200, 200));
            _flash = UIKit.Panel(_root, "flash", new Color(1f, 1f, 1f, 0f), false);
            _flash.rectTransform.Stretch();
            _flash.raycastTarget = false;

            AudioService.Play(dramatic ? Sfx.Chest : Sfx.Reward, 0.8f);
        }

        /// <summary>Gacha egg: art gacha_egg, else a striped purple egg with a gold star.</summary>
        static void Egg(RectTransform holder)
        {
            var art = Art.Icon("gacha_egg");
            if (art != null) { UIKit.Icon(holder, "egg", art, Color.white).rectTransform.Stretch(20, 20, 10, 10); return; }
            var shell = UIKit.Icon(holder, "shell", Art.Circle, Art.Hex(0x9B5CFF));
            shell.preserveAspect = false;
            shell.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(290, 360));
            for (int k = 0; k < 2; k++)
            {
                var band = UIKit.Panel(shell.transform, "band", Art.Hex(k == 0 ? 0xFFD23F : 0xFF7AC8), false).rectTransform;
                band.anchorMin = new Vector2(0.06f, 0.42f + k * 0.16f);
                band.anchorMax = new Vector2(0.94f, 0.48f + k * 0.16f);
                band.offsetMin = band.offsetMax = Vector2.zero;
            }
            var shine = UIKit.Icon(shell.transform, "shine", Art.Circle, new Color(1f, 1f, 1f, 0.35f));
            shine.preserveAspect = false;
            shine.rectTransform.Place(new Vector2(0.3f, 0.75f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70, 100));
            var star = Art.Icon("star");
            UIKit.Icon(shell.transform, "star", star != null ? star : Art.Diamond, star != null ? Color.white : Art.Hex(0xFFD23F))
                .rectTransform.Place(new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 110));
        }

        Card BuildCard(RectTransform parent, Reward r, int tier, ICollection<string> newlyUnlocked, Vector2 pos, Vector2 size, int index)
        {
            var c = new Card { tier = tier };
            c.root = UIKit.Rect("card" + index, parent).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            c.root.localScale = Vector3.zero;
            c.dealAt = index * 0.07f;
            var color = TierColor(tier);

            // Rays behind rare cards (shown on flip)
            if (tier >= 2)
            {
                c.rays = UIKit.Icon(c.root, "rays", Art.Rays, new Color(color.r, color.g, color.b, 0.55f)).rectTransform;
                c.rays.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size * 2.4f);
                UIKit.Motion(c.rays).Spin(40f);
                c.rays.gameObject.SetActive(false);
            }

            // Back
            var backContent = Deco.Frame(c.root, "back", Art.Hex(0xB46BFF), Art.Hex(0x4A2A80), 7f);
            c.back = (RectTransform)backContent.parent;
            var backArt = Art.Icon("card_back");
            if (backArt != null)
            {
                var img = UIKit.Icon(backContent, "art", backArt, Color.white);
                img.preserveAspect = false;
                img.rectTransform.Stretch();
            }
            else
            {
                var star = Art.Icon("star");
                UIKit.Icon(backContent, "star", star != null ? star : Art.Diamond, star != null ? new Color(1f, 1f, 1f, 0.9f) : Art.Hex(0xFFD23F))
                    .rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(size.x * 0.5f, size.x * 0.5f));
                UIKit.Outlined(UIKit.Label(backContent, "?", 54, Palette.Text), 3f).rectTransform.Band(false, 70, 10);
            }

            // Front
            var front = Deco.Frame(c.root, "front", color, Art.Hex(0x221836), 7f);
            c.front = (RectTransform)front.parent;
            var g = UIKit.Icon(front, "glow", Art.Glow, new Color(color.r, color.g, color.b, 0.6f));
            g.preserveAspect = false;
            g.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -size.x * 0.42f), new Vector2(size.x * 1.1f, size.x));
            float icon = size.x * 0.72f;
            Icons.Reward(front, r, _app.Content, icon).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(icon, icon));

            string amount = r.type == RewardType.Chest ? "" : "+" + r.amount;
            UIKit.Outlined(UIKit.Label(front, amount, size.x < 200 ? 38 : 44, Palette.Text), 2.5f).rectTransform.Band(false, 54, size.y * 0.2f);
            string name = "";
            if (r.type == RewardType.Shards) { var def = _app.Content.Unit(r.id); name = def != null ? Loc.UnitName(def) : r.id; }
            else if (r.type == RewardType.Chest) name = Loc.T("chest." + r.id);
            else if (r.type == RewardType.Gold) name = Loc.T("reward.name.gold");
            else if (r.type == RewardType.Gems) name = Loc.T("reward.name.gems");
            else if (r.type == RewardType.PassXp) name = Loc.T("reward.name.xp");
            var nameBg = UIKit.Panel(front, "nameBg", Color.Lerp(color, Color.black, 0.45f)).rectTransform;
            nameBg.Band(false, size.y * 0.17f, 4, 4);
            UIKit.Label(nameBg, name, size.x < 200 ? 20 : 24, Palette.Text).rectTransform.Stretch(4, 4, 0, 0);
            if (r.type == RewardType.Shards && newlyUnlocked != null && newlyUnlocked.Contains(r.id))
            {
                var tag = Deco.Tag(c.front, Loc.T("reward.new"), Palette.Red);
                tag.sizeDelta = new Vector2(120, 42);
                UIKit.Motion(tag).Pulse(0.08f, 6f);
            }
            c.front.gameObject.SetActive(false);
            return c;
        }

        static int Tier(Reward r)
        {
            switch (r.type)
            {
                case RewardType.Shards:
                    var def = GameApp.Instance.Content.Unit(r.id);
                    return def != null ? (int)def.rarity : 0;
                case RewardType.Gems: return 1;
                case RewardType.Chest: return r.id == "magic" ? 3 : (r.id == "gold" ? 2 : (r.id == "silver" ? 1 : 0));
                default: return 0;
            }
        }

        static Color TierColor(int tier) { return Art.RarityColor((Rarity)Mathf.Clamp(tier, 0, (int)Rarity.SSS)); }

        void Skip()
        {
            if (_phase == 3) return;
            if (_phase == 0) { _t = _introEnd; return; }
            // Reveal everything at once.
            foreach (var c in _cards)
            {
                c.dealAt = -10f;
                if (!c.shown) { c.flipAt = _t - FlipTime; }
            }
            _phase = 2;
            _flipIndex = _cards.Count;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _t += dt;

            // Flash + ring fade
            if (_flash.color.a > 0f) _flash.color = new Color(_flash.color.r, _flash.color.g, _flash.color.b, Mathf.Max(0f, _flash.color.a - dt * 2.5f));
            var ringImg = _ring.GetComponent<Image>();
            if (ringImg.color.a > 0f)
            {
                float a = Mathf.Max(0f, ringImg.color.a - dt * 1.6f);
                ringImg.color = new Color(1f, 1f, 1f, a);
                float s = 1f + (1f - a) * 6f;
                _ring.localScale = new Vector3(s, s, 1f);
            }

            if (_phase == 0)
            {
                // Shake harder and harder, then burst.
                float k = Mathf.Clamp01(_t / _introEnd);
                float amp = 3f + 18f * k * k;
                _intro.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_t * 45f) * amp);
                float sc = 1f + 0.15f * k + 0.03f * Mathf.Sin(_t * 30f);
                _intro.localScale = new Vector3(sc, sc, 1f);
                if (_t >= _introEnd)
                {
                    _intro.gameObject.SetActive(false);
                    _flash.color = new Color(1f, 1f, 1f, 0.95f);
                    ringImg.color = Color.white;
                    _ring.localScale = Vector3.one;
                    AudioService.Play(Sfx.Merge);
                    _phase = 1;
                    _phaseStart = _t;
                }
                return;
            }

            // Deal
            for (int i = 0; i < _cards.Count; i++)
            {
                var c = _cards[i];
                if (c.flipAt >= 0f) continue;
                float k = (_t - _phaseStart - c.dealAt) / 0.28f;
                float s = k <= 0f ? 0f : Deco.EaseOutBack(k);
                c.root.localScale = new Vector3(s, s, 1f);
            }
            if (_phase == 1 && _cards.Count > 0 && _t - _phaseStart > _cards[_cards.Count - 1].dealAt + 0.4f)
            {
                _phase = 2;
                _nextFlip = _t;
            }

            // Flip one by one (rare cards keep the player waiting a little longer).
            if (_phase == 2)
            {
                if (_flipIndex < _cards.Count && _t >= _nextFlip)
                {
                    var c = _cards[_flipIndex++];
                    c.flipAt = _t;
                    _nextFlip = _t + (c.tier >= 4 ? 1.4f : (c.tier >= 2 ? 0.6f : 0.2f));   // SS / SSS: a real moment
                }
                bool allDone = _flipIndex >= _cards.Count;
                foreach (var c in _cards)
                {
                    if (c.flipAt < 0f) continue;
                    float k = Mathf.Clamp01((_t - c.flipAt) / FlipTime);
                    float sx = k < 0.5f ? 1f - k * 2f : (k - 0.5f) * 2f;
                    if (k >= 0.5f && !c.shown)
                    {
                        c.shown = true;
                        c.back.gameObject.SetActive(false);
                        c.front.gameObject.SetActive(true);
                        if (c.rays != null) c.rays.gameObject.SetActive(true);
                        if (c.tier >= 4)
                        {
                            AudioService.Play(Sfx.Win, 1f);
                            AudioService.Play(Sfx.Chest, 1f);
                            var tc = TierColor(c.tier);
                            _flash.color = new Color(tc.r, tc.g, tc.b, 0.75f);
                            if (c.rays != null) { c.rays.localScale = Vector3.one * 1.4f; if (c.tier >= 5) c.rays.gameObject.AddComponent<RainbowTint>(); }
                        }
                        else if (c.tier >= 2) { AudioService.Play(Sfx.Chest, 0.8f); _flash.color = new Color(1f, 1f, 1f, 0.35f); }
                        else AudioService.Play(Sfx.Reward, 0.35f);
                    }
                    float punchAmp = c.tier >= 4 ? 0.18f : 0.06f;
                    float punch = c.tier >= 2 && k >= 1f ? 1f + punchAmp * Mathf.Max(0f, 1f - (_t - c.flipAt - FlipTime) * (c.tier >= 4 ? 1.5f : 3f)) : 1f;
                    c.root.localScale = new Vector3(Mathf.Max(0.02f, sx) * punch, punch, 1f);
                    if (k < 1f) allDone = false;
                }
                if (allDone)
                {
                    _phase = 3;
                    _hint.gameObject.SetActive(false);
                    _okRt.gameObject.SetActive(true);
                    UIKit.Motion(_okRt).PopIn(0.1f, 0.3f);
                }
            }
        }
    }
}
