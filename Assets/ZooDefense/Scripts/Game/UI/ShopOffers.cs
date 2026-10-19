using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>Shop: special-offer bundles, the monthly card and gem-pack bonus badges.</summary>
    public sealed partial class ShopScreen
    {
        void BuildOffers(RectTransform list, long now)
        {
            var meta = App.Meta;
            var visible = new List<BundleDef>();
            foreach (var b in ShopCatalog.Bundles) if (meta.BundleVisible(b)) visible.Add(b);
            Header(list, Loc.T("shop.offers"));
            foreach (var b in visible) BundleCard(list, b, now);
            MonthlyBanner(list, now);
        }

        // ------------------------------------------------------------------ bundles

        void BundleCard(RectTransform list, BundleDef b, long now)
        {
            var meta = App.Meta;
            bool canBuy = meta.CanBuyBundle(b, now);
            var color = Art.Hex(b.color);
            var holder = UIKit.Rect("bundle_" + b.id, list).Band(true, 330, _y, 8);
            _y += 354;
            var c = Deco.Frame(holder, "frame", color, Color.Lerp(color, CardFill, 0.8f), 8f);

            // Main icon on a glow
            var glow = UIKit.Icon(c, "glow", Art.Glow, new Color(color.r, color.g, color.b, 0.6f));
            glow.preserveAspect = false;
            glow.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(150, 10), new Vector2(320, 300));
            var main = b.icon == "gems" || b.icon == "gold" ? Icons.Currency(c, b.icon).rectTransform : Icons.Chest(c, b.icon);
            main.Place(new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(150, 16), new Vector2(220, 200));
            if (canBuy) _pulse.Add(main);

            // Title + limit line
            UIKit.Outlined(UIKit.Label(c, Loc.T("bundle." + b.id), 38, Palette.Primary, TextAnchor.UpperLeft), 2.5f).rectTransform
                .Stretch(300, 20, 18, 250);
            UIKit.Label(c, LimitText(b, now, canBuy), 24, Palette.TextDim, TextAnchor.UpperLeft).rectTransform.Stretch(300, 20, 66, 210);

            // Reward chips
            for (int i = 0; i < b.rewards.Count && i < 4; i++)
            {
                var r = b.rewards[i];
                var chip = UIKit.Panel(c, "chip" + i, new Color(0f, 0f, 0f, 0.35f)).rectTransform;
                chip.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(300 + i * 150, 20), new Vector2(138, 150));
                Icons.Reward(chip, r, App.Content, 80).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(84, 84));
                UIKit.Outlined(UIKit.Label(chip, ChipText(r), 26, Palette.Text), 2f).rectTransform.Band(false, 46, 6, 2);
            }

            // Buy button
            var btn = UIKit.ChunkyButton(c, "buy", "", canBuy ? Palette.Green : Palette.Disabled, null, 32);
            btn.interactable = canBuy;
            btn.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18, 20), new Vector2(200, 112));
            var face = btn.transform.Find("face");
            if (!canBuy) face.GetComponentInChildren<Text>().text = Loc.T("shop.sold");
            else if (b.priceGems > 0)
            {
                Icons.Currency(face, "gems").rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-4, 0), new Vector2(54, 54));
                UIKit.Label(face, b.priceGems.ToString(), 34, Palette.Text, TextAnchor.MiddleLeft).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(100, 60));
            }
            else face.GetComponentInChildren<Text>().text = Services.Store.Price(b.id, b.price);
            btn.onClick.AddListener(() => BuyBundle(b));

            if (b.valuePercent > 0) Deco.Tag(holder, Loc.F("shop.value", b.valuePercent / 100), Palette.Red);
        }

        string LimitText(BundleDef b, long now, bool canBuy)
        {
            switch (b.limit)
            {
                case BundleLimit.Daily:
                    return canBuy ? Loc.T("shop.limit_daily") : Loc.F("shop.back_in", Fmt.Duration(MetaService.SecondsPerDay - now % MetaService.SecondsPerDay));
                case BundleLimit.Weekly:
                    return canBuy ? Loc.T("shop.limit_weekly") : Loc.F("shop.back_in", Fmt.Duration(GameCalendar.WeekEnd(now) - now));
                default:
                    return Loc.T("shop.limit_once");
            }
        }

        static string ChipText(Reward r)
        {
            string n = r.amount >= 10000 ? (r.amount / 1000) + "K" : r.amount.ToString();
            return r.type == RewardType.Chest ? "x" + r.amount : n;
        }

        void BuyBundle(BundleDef b)
        {
            string title = Loc.T("bundle." + b.id);
            if (b.priceGems > 0)
            {
                Modal.Confirm(App, title, Loc.F("shop.confirm_gems", b.priceGems), Loc.F("shop.gems_price", b.priceGems), () =>
                {
                    var locked = Modal.LockedUnits(App);
                    var rewards = App.Meta.BuyBundleWithGems(b.id, App.Now);
                    if (rewards == null) { NotEnough(true); return; }
                    Bought(title, rewards, locked, true, b.icon == "gems" || b.icon == "gold" ? null : b.icon);
                });
                return;
            }
            var lockedBefore = Modal.LockedUnits(App);
            Services.Store.Purchase(b.id, rewards =>
            {
                if (rewards == null) { App.Toast(Loc.T("shop.purchase_failed")); return; }
                Bought(title, rewards, lockedBefore, true, b.icon == "gems" || b.icon == "gold" ? null : b.icon);
            });
        }

        // ------------------------------------------------------------------ monthly card

        void MonthlyBanner(RectTransform list, long now)
        {
            var meta = App.Meta;
            var m = ShopCatalog.MonthlyCard;
            var color = Art.Hex(0x2EC4B6);
            var holder = UIKit.Rect("monthly", list).Band(true, 300, _y, 8);
            _y += 324;
            var c = Deco.Frame(holder, "frame", color, Color.Lerp(color, CardFill, 0.8f), 8f);
            var glow = UIKit.Icon(c, "glow", Art.Glow, new Color(color.r, color.g, color.b, 0.6f));
            glow.preserveAspect = false;
            glow.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(150, 0), new Vector2(300, 280));
            for (int k = 0; k < 3; k++)
                Icons.Currency(c, "gems").rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(110 + k * 40, 20 - Mathf.Abs(k - 1) * 18), new Vector2(120, 120));
            var cal = UIKit.Panel(c, "days", new Color(0f, 0f, 0f, 0.55f)).rectTransform;
            cal.Place(new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(150, -90), new Vector2(170, 52));
            UIKit.Label(cal, Loc.F("shop.monthly_days", m.days), 26, Palette.Primary).rectTransform.Stretch();

            UIKit.Outlined(UIKit.Label(c, Loc.T("shop.monthly"), 38, Palette.Primary, TextAnchor.UpperLeft), 2.5f).rectTransform.Stretch(300, 20, 18, 220);
            UIKit.Label(c, Loc.F("shop.monthly_text", m.instantGems, m.dailyGems, m.days, m.instantGems + m.dailyGems * m.days), 26, Palette.Text, TextAnchor.UpperLeft)
                .rectTransform.Stretch(300, 240, 68, 20);
            if (meta.MonthlyActive(now))
                UIKit.Label(c, Loc.F("shop.monthly_left", meta.MonthlyDaysLeft(now)), 24, Art.Hex(0x7CFFB2), TextAnchor.LowerLeft).rectTransform.Stretch(300, 240, 200, 20);

            var btn = UIKit.ChunkyButton(c, "buy", Services.Store.Price(m.id, m.price), Palette.Green, BuyMonthly, 32);
            btn.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18, 20), new Vector2(200, 112));
            Deco.Tag(holder, Loc.F("shop.value", 10), Palette.Red);
        }

        void MonthlyClaimTile(RectTransform row, int index, int count)
        {
            var meta = App.Meta;
            bool ready = meta.CanClaimMonthly(App.Now);
            Offer(row, index, count, Art.Hex(0x2EC4B6), "gems", 2, Loc.F("reward.gems", ShopCatalog.MonthlyCard.dailyGems),
                ready ? Loc.T("shop.claim") : Loc.T("shop.claimed"), null, ready, ClaimMonthly, Loc.T("shop.monthly_tag"));
        }

        void ClaimMonthly()
        {
            if (!App.Meta.ClaimMonthly(App.Now)) return;
            Bought(Loc.T("shop.monthly"), new List<Reward> { new Reward(RewardType.Gems, ShopCatalog.MonthlyCard.dailyGems) }, null);
        }

        void BuyMonthly()
        {
            var m = ShopCatalog.MonthlyCard;
            Services.Store.Purchase(m.id, rewards =>
            {
                if (rewards == null) { App.Toast(Loc.T("shop.purchase_failed")); return; }
                Bought(Loc.T("shop.monthly"), rewards, null);
            });
        }

        // ------------------------------------------------------------------ gem pack badges

        /// <summary>Extra gems per dollar compared with the smallest pack, rounded to 5%.</summary>
        int GemBonusPercent(int index)
        {
            var packs = App.MetaConfig.gemPacks;
            float baseRate = Rate(packs[0]), rate = Rate(packs[index]);
            if (baseRate <= 0f || rate <= 0f) return 0;
            int pct = Mathf.RoundToInt((rate / baseRate - 1f) * 20f) * 5;
            return pct >= 5 ? pct : 0;
        }

        static float Rate(GemPack p)
        {
            float price;
            var digits = new System.Text.StringBuilder();
            foreach (var ch in p.price) if (char.IsDigit(ch) || ch == '.') digits.Append(ch);
            if (!float.TryParse(digits.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out price) || price <= 0f) return 0f;
            return p.gems / price;
        }

        static void BonusChip(RectTransform cell, string text)
        {
            if (cell == null) return;
            var chip = UIKit.Panel(cell, "bonus", Art.Hex(0x7A3FD1)).rectTransform;
            chip.Place(new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(-6, -6), new Vector2(120, 42));
            chip.localRotation = Quaternion.Euler(0f, 0f, -6f);
            UIKit.Label(chip, text, 22, Palette.Text).rectTransform.Stretch(2, 2, 0, 0);
        }
    }
}
