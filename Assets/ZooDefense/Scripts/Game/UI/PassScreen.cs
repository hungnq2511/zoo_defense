using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>
    /// Season pass tab: a vertical "road" of 30 tiers. Each tier has a node on the centre track (filled when
    /// reached), a free reward card on the left and a premium (gold) card on the right.
    /// XP comes from matches and missions.
    /// </summary>
    public sealed class PassScreen : ScreenBase
    {
        const float RowHeight = 236f;
        static float _scroll = -1f;
        static readonly Color CardFill = Art.Hex(0x261C3C);
        static readonly Color Gold = Art.Hex(0xFFC93C);
        ScrollRect _scrollRect;
        readonly List<RectTransform> _claimable = new List<RectTransform>();
        RectTransform _premiumButton;
        float _t;

        protected override void Build()
        {
            var meta = App.Meta;
            var cfg = App.MetaConfig;
            long now = App.Now;
            meta.Refresh(now);
            int tier = meta.PassTier;
            int maxTier = cfg.passTiers.Count;

            Deco.ScreenBackground(Root, App, Art.Hex(0x9B5CFF));
            new CurrencyBar(Root, App, false);
            Deco.Ribbon(Root, Loc.F("pass.season", meta.Season(now) + 1), Art.Hex(0x8A4FE0), 46)
                .Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -CurrencyBar.Height - 14), new Vector2(640, 96));

            // ---- Header card: current tier badge, XP bar, days left, premium status
            var head = Deco.Frame(Root, "head", Gold, Art.Hex(0x2E1F4D), 8f);
            var headRoot = head.parent as RectTransform;
            headRoot.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -CurrencyBar.Height - 124), new Vector2(1040, 300));

            var badge = UIKit.Icon(head, "tierBadge", Art.Circle, Art.Hex(0x9B5CFF)).rectTransform;
            badge.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20, -20), new Vector2(150, 150));
            UIKit.Icon(badge, "ring", Art.Ring, Gold).rectTransform.Stretch(-6, -6, -6, -6);
            UIKit.Label(badge, Loc.T("pass.tier_short"), 22, Palette.TextDim).rectTransform.Band(true, 30, 22);
            Deco.Title(badge, tier.ToString(), 64, Palette.Text).rectTransform.Stretch(0, 0, 40, 10);

            int per = cfg.passXpPerTier;
            bool max = tier >= maxTier;
            UIKit.Label(head, Loc.F("pass.tier", tier, maxTier), 36, Palette.Text, TextAnchor.MiddleLeft).rectTransform.Stretch(190, 20, 24, 210);
            var xp = new Bar(head, Art.Hex(0x9B5CFF), 26);
            xp.Root.Stretch(190, 24, 86, 150);
            xp.Set(max ? 1f : (meta.State.passXp % per) / (float)per, max ? Loc.T("pass.max") : Loc.F("pass.xp", meta.State.passXp % per, per));
            Icons.Currency(xp.Root, "xp").rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-6, 0), new Vector2(64, 64));
            var ends = Deco.Pill(head, null, Loc.F("pass.ends", Fmt.Duration(meta.SeasonSecondsLeft(now))), 24);
            ends.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20, 18), new Vector2(330, 52));

            if (meta.State.passPremium)
            {
                var owned = Deco.Pill(head, null, Loc.T("pass.premium_owned"), 28);
                owned.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20, 14), new Vector2(420, 70));
                owned.GetComponent<Image>().color = Color.Lerp(Gold, Color.black, 0.45f);
            }
            else
            {
                var buy = UIKit.ChunkyButton(head, "premium", "", Gold, BuyPremium, 30);
                _premiumButton = buy.GetComponent<RectTransform>();
                _premiumButton.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16, 10), new Vector2(520, 104));
                var face = buy.transform.Find("face");
                UIKit.Label(face, Loc.T("pass.unlock_premium"), 30, Palette.Text, TextAnchor.MiddleLeft).rectTransform.Stretch(24, 170, 0, 0);
                Icons.Currency(face, "gems").rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-96, 0), new Vector2(56, 56));
                UIKit.Label(face, cfg.passPremiumGems.ToString(), 30, Palette.Text, TextAnchor.MiddleLeft).rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-6, 0), new Vector2(90, 60));
            }

            // ---- Column titles
            float listTop = CurrencyBar.Height + 124 + 316;
            var cols = UIKit.Rect("cols", Root).Band(true, 50, listTop, 20);
            UIKit.Label(cols, Loc.T("pass.free"), 32, Palette.TextDim).rectTransform.Place(new Vector2(0.23f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 50));
            var premTitle = UIKit.Label(cols, Loc.T("pass.premium"), 32, Gold);
            premTitle.rectTransform.Place(new Vector2(0.77f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 50));

            // ---- The road
            var area = UIKit.Rect("tiers", Root).Stretch(20, 20, listTop + 56, NavBar.Height + 6);
            var list = ScrollKit.Vertical(area);
            _scrollRect = area.GetComponent<ScrollRect>();
            float height = maxTier * RowHeight + 30;
            // Centre track: background line + filled part up to the reached tier
            var track = UIKit.Panel(list, "track", new Color(0f, 0f, 0f, 0.45f), false).rectTransform;
            track.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -RowHeight * 0.5f), new Vector2(22, (maxTier - 1) * RowHeight));
            if (tier > 0)
            {
                var fill = UIKit.Panel(list, "trackFill", Art.Hex(0x9B5CFF), false).rectTransform;
                fill.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -RowHeight * 0.5f), new Vector2(14, Mathf.Max(0, Mathf.Min(tier, maxTier) - 1) * RowHeight));
            }
            for (int t = 1; t <= maxTier; t++) TierRow(list, t, tier);
            ScrollKit.SetHeight(list, height);

            // First visit: scroll to the current tier.
            if (_scroll < 0f)
            {
                float target = Mathf.Max(0, tier - 1) * RowHeight;
                float view = Mathf.Max(1f, height - 900f);
                _scroll = Mathf.Clamp01(1f - target / view);
            }
            _scrollRect.verticalNormalizedPosition = _scroll;

            NavBar.Build(Root, App, Tab.Pass);
        }

        public override void Tick(float dt)
        {
            _t += dt;
            float s = 1f + 0.04f * Mathf.Sin(_t * 5f);
            foreach (var c in _claimable) if (c != null) c.localScale = new Vector3(s, s, 1f);
            if (_premiumButton != null) { float p = 1f + 0.02f * Mathf.Sin(_t * 3f); _premiumButton.localScale = new Vector3(p, p, 1f); }
        }

        public override void Close() { if (_scrollRect != null) _scroll = _scrollRect.verticalNormalizedPosition; }

        void TierRow(RectTransform list, int t, int reached)
        {
            var cfg = App.MetaConfig;
            var def = cfg.passTiers[t - 1];
            bool done = t <= reached;
            float y = -(t - 1) * RowHeight;

            // Node on the track
            var node = UIKit.Icon(list, "node" + t, Art.Circle, done ? Art.Hex(0x9B5CFF) : Art.Hex(0x3A2D57)).rectTransform;
            node.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, y - RowHeight * 0.5f), new Vector2(96, 96));
            UIKit.Icon(node, "ring", Art.Ring, done ? Gold : Art.Hex(0x5E5670)).rectTransform.Stretch(-4, -4, -4, -4);
            UIKit.Label(node, t.ToString(), 40, done ? Palette.Text : Palette.TextDim).rectTransform.Stretch();

            RewardCard(list, t, def.free, false, new Vector2(-280, y - 14));
            RewardCard(list, t, def.premium, true, new Vector2(280, y - 14));
        }

        void RewardCard(RectTransform list, int t, Reward r, bool premium, Vector2 pos)
        {
            var meta = App.Meta;
            bool claimed = (premium ? meta.State.passClaimedPremium : meta.State.passClaimedFree).Contains(t);
            bool can = meta.CanClaimPass(t, premium);
            bool locked = premium && !meta.State.passPremium;
            bool reached = t <= meta.PassTier;

            var holder = UIKit.Rect("card" + t + (premium ? "p" : "f"), list).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), pos, new Vector2(400, RowHeight - 28));
            var border = premium ? Gold : (reached ? Art.Hex(0x7FD3FF) : Art.Hex(0x5E5670));
            var c = Deco.Frame(holder, "frame", border, premium ? Art.Hex(0x3A2A1E) : CardFill);

            var glow = UIKit.Icon(c, "glow", Art.Glow, new Color(border.r, border.g, border.b, can ? 0.75f : 0.3f));
            glow.preserveAspect = false;
            glow.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-20, 0), new Vector2(220, 200));
            Icons.Reward(c, r, App.Content, 130).Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14, 6), new Vector2(130, 130));
            UIKit.Label(c, Fmt.Reward(r, App.Content), 28, Palette.Text, TextAnchor.UpperLeft).rectTransform.Stretch(156, 10, 16, 90);

            if (claimed)
            {
                UIKit.Panel(c, "shade", new Color(0f, 0f, 0f, 0.45f)).rectTransform.Stretch();
                Deco.Check(c, 80).anchoredPosition = new Vector2(0, 0);
                return;
            }
            if (locked)
            {
                UIKit.Panel(c, "shade", new Color(0f, 0f, 0f, 0.35f)).rectTransform.Stretch();
                var lk = Deco.Lock(c, 54);
                lk.anchorMin = lk.anchorMax = new Vector2(1f, 0f);
                lk.anchoredPosition = new Vector2(-50, 50);
                return;
            }
            if (can)
            {
                var btn = UIKit.ChunkyButton(c, "claim", Loc.T("pass.claim"), Palette.Green, null, 28);
                btn.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-8, 8), new Vector2(200, 84));
                _claimable.Add(btn.GetComponent<RectTransform>());
                btn.onClick.AddListener(() =>
                {
                    var lockedUnits = Modal.LockedUnits(App);
                    var rewards = meta.ClaimPass(t, premium);
                    if (rewards == null) return;
                    App.SaveProfile();
                    App.Show(new PassScreen());
                    Modal.Rewards(App, Loc.F("pass.tier_reward", t), rewards, lockedUnits);
                });
            }
            else UIKit.Label(c, Loc.F("pass.need_tier", t), 24, Palette.TextDim, TextAnchor.LowerRight).rectTransform.Stretch(10, 16, 10, 12);
        }

        void BuyPremium()
        {
            int price = App.MetaConfig.passPremiumGems;
            Modal.Confirm(App, Loc.T("pass.premium"), Loc.F("pass.premium_text", price), Loc.F("shop.gems_price", price), () =>
            {
                if (!App.Meta.BuyPremiumPass())
                {
                    AudioService.Play(Sfx.Error);
                    App.Toast(Loc.T("shop.not_enough_gems"));
                    return;
                }
                App.SaveProfile();
                AudioService.Play(Sfx.Buy);
                Services.Analytics.LogEvent("pass_premium");
                App.Show(new PassScreen());
                App.Toast(Loc.T("pass.premium_owned"));
            });
        }
    }
}
