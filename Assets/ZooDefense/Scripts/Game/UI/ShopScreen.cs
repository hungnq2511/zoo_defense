using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>
    /// Shop tab: free daily gifts, 3 daily shard deals (gold), the animal gacha (gems),
    /// chests and gold for gems, and gem packs (real money through Services.Store).
    /// </summary>
    public sealed class ShopScreen : ScreenBase
    {
        static float _scroll = 1f;
        ScrollRect _scrollRect;
        CurrencyBar _bar;
        float _y;   // layout cursor inside the scroll content (grows downward)

        static readonly Color CardFill = Art.Hex(0x261C3C);
        RectTransform _raysA, _freeGlow;
        readonly List<RectTransform> _pulse = new List<RectTransform>();
        float _t;

        protected override void Build()
        {
            var meta = App.Meta;
            long now = App.Now;
            meta.Refresh(now);
            var cfg = App.MetaConfig;

            Deco.ScreenBackground(Root, App, Art.Hex(0xFFB020));
            _bar = new CurrencyBar(Root, App, false);
            float top = Deco.Header(Root, Loc.T("nav.shop"), Art.Hex(0xE58A1F));
            var area = UIKit.Rect("shop", Root).Stretch(20, 20, top, NavBar.Height + 6);
            var list = ScrollKit.Vertical(area);
            _scrollRect = area.GetComponent<ScrollRect>();
            _y = 6;

            // ---- Free gifts
            Header(list, Loc.T("shop.free"));
            var free = Row(list, 300);
            bool goldReady = meta.CanClaimFreeGold(now), gemsReady = meta.CanClaimAdGems(now) && Services.Ads.IsRewardedReady;
            Offer(free, 0, 2, Art.Hex(0x4CC46B), "gold", 1, Loc.F("reward.gold", cfg.freeDailyGold),
                goldReady ? Loc.T("shop.claim") : Loc.T("shop.claimed"), null, goldReady, ClaimFreeGold, goldReady ? Loc.T("shop.free_tag") : null);
            Offer(free, 1, 2, Art.Hex(0x3D8BFF), "gems", 1, Loc.F("reward.gems", cfg.adGems),
                meta.CanClaimAdGems(now) ? Loc.T("shop.watch_ad") : Loc.T("shop.claimed"), null, gemsReady, ClaimAdGems, null);

            // ---- Daily deals
            Header(list, Loc.F("shop.daily", Fmt.Duration(MetaService.SecondsPerDay - now % MetaService.SecondsPerDay)));
            var deals = Row(list, 400);
            for (int i = 0; i < meta.State.dealUnits.Count; i++) DealTile(deals, i, meta.State.dealUnits.Count);

            // ---- Gacha banner
            Header(list, Loc.T("shop.gacha"));
            GachaBanner(list);

            // ---- Chests
            Header(list, Loc.T("shop.chests"));
            var chests = new List<ChestDef>();
            foreach (var c in cfg.chests) if (c.gemPrice > 0) chests.Add(c);
            var chestRow = Row(list, 380);
            for (int i = 0; i < chests.Count; i++) ChestTile(chestRow, i, chests.Count, chests[i]);

            // ---- Gold for gems
            Header(list, Loc.T("shop.gold"));
            var goldRow = Row(list, 330);
            Offer(goldRow, 0, 2, Art.Hex(0xFFC93C), "gold", 2, Loc.F("reward.gold", cfg.goldPackSmallGold), cfg.goldPackSmallGems.ToString(), "gems", true, () => BuyGold(false), null);
            Offer(goldRow, 1, 2, Art.Hex(0xFFC93C), "gold", 4, Loc.F("reward.gold", cfg.goldPackBigGold), cfg.goldPackBigGems.ToString(), "gems", true, () => BuyGold(true), Loc.T("shop.best_value"));

            // ---- Gem packs (real money)
            Header(list, Loc.T("shop.gems"));
            var packs = cfg.gemPacks;
            var gemRow = Row(list, 330);
            for (int i = 0; i < packs.Count; i++)
            {
                var pack = packs[i];
                Offer(gemRow, i, packs.Count, Art.Hex(0x5BD6FF), "gems", 1 + i * 2, Loc.F("reward.gems", pack.gems), pack.price, null, true, () => BuyGems(pack),
                    i == packs.Count - 1 ? Loc.T("shop.best_value") : (i == 1 ? Loc.T("shop.popular") : null));
            }
            UIKit.Label(list, Loc.T("shop.test_store"), 24, Palette.TextDim).rectTransform.Band(true, 40, _y, 10);
            _y += 60;

            ScrollKit.SetHeight(list, _y);
            _scrollRect.verticalNormalizedPosition = _scroll;
            NavBar.Build(Root, App, Tab.Shop);
        }

        public override void Tick(float dt)
        {
            _t += dt;
            if (_raysA != null) _raysA.localRotation = Quaternion.Euler(0f, 0f, -_t * 10f);
            float s = 1f + 0.04f * Mathf.Sin(_t * 4f);
            foreach (var p in _pulse) if (p != null) p.localScale = new Vector3(s, s, 1f);
        }

        public override void Close() { if (_scrollRect != null) _scroll = _scrollRect.verticalNormalizedPosition; }

        void Rebuild() { App.Show(new ShopScreen()); }

        // ------------------------------------------------------------------ layout helpers

        void Header(RectTransform list, string text)
        {
            Deco.Section(list, text, _y, Palette.Primary);
            _y += 74;
        }

        RectTransform Row(RectTransform list, float height)
        {
            var row = UIKit.Rect("row", list).Band(true, height, _y);
            _y += height + 24;
            return row;
        }

        static RectTransform Cell(RectTransform row, int index, int count)
        {
            var cell = UIKit.Rect("cell" + index, row);
            cell.anchorMin = new Vector2(index / (float)count, 0f);
            cell.anchorMax = new Vector2((index + 1) / (float)count, 1f);
            cell.offsetMin = new Vector2(8, 0);
            cell.offsetMax = new Vector2(-8, 0);
            return cell;
        }

        /// <summary>
        /// Generic offer card: coloured frame, a pile of `stack` icons on a glow, the amount, and a chunky price
        /// button (priceIcon = currency icon on the button, null = plain text such as "$4.99").
        /// </summary>
        void Offer(RectTransform row, int index, int count, Color color, string icon, int stack, string label,
                   string price, string priceIcon, bool enabled, System.Action onBuy, string tag)
        {
            var cell = Cell(row, index, count);
            var c = Deco.Frame(cell, "frame", color, CardFill);
            var glow = UIKit.Icon(c, "glow", Art.Glow, new Color(color.r, color.g, color.b, 0.55f));
            glow.preserveAspect = false;
            glow.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, 0), new Vector2(240, 190));
            // A small pile: bigger packs show more icons.
            stack = Mathf.Clamp(stack, 1, 5);
            for (int k = 0; k < stack; k++)
            {
                float ox = (k - (stack - 1) * 0.5f) * 34f, oy = -16 - Mathf.Abs(k - (stack - 1) * 0.5f) * 10f;
                Icons.Currency(c, icon).rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(ox, oy), new Vector2(110, 110));
            }
            UIKit.Label(c, label, 32, Palette.Text).rectTransform.Band(true, 44, 130, 6);
            var btn = UIKit.ChunkyButton(c, "buy", priceIcon == null ? price : "", enabled ? Palette.Green : Palette.Disabled, onBuy, 30);
            btn.interactable = enabled;
            btn.GetComponent<RectTransform>().Band(false, 88, 8, 10);
            if (priceIcon != null)
            {
                var face = btn.transform.Find("face");
                Icons.Currency(face, priceIcon).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-6, 0), new Vector2(54, 54));
                UIKit.Label(face, price, 32, Palette.Text, TextAnchor.MiddleLeft).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0, 0), new Vector2(160, 60));
            }
            if (tag != null) Deco.Tag(cell, tag, Palette.Red);
            if (enabled && priceIcon == null && onBuy == (System.Action)ClaimFreeGold) _pulse.Add(btn.GetComponent<RectTransform>());
        }

        void DealTile(RectTransform row, int index, int count)
        {
            var meta = App.Meta;
            var def = App.Content.Unit(meta.State.dealUnits[index]);
            int shards, gold;
            meta.DealPrice(index, out shards, out gold);
            bool bought = meta.State.dealBought[index];
            var rarity = Art.RarityColor(def.rarity);
            var cell = Cell(row, index, count);
            var c = Deco.Frame(cell, "frame", rarity, CardFill);
            var glow = UIKit.Icon(c, "glow", Art.Glow, new Color(rarity.r, rarity.g, rarity.b, 0.5f));
            glow.preserveAspect = false;
            glow.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(260, 230));
            Icons.Unit(c, def, 1, 200).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -2), new Vector2(200, 200));
            var amount = UIKit.Panel(c, "amount", new Color(0f, 0f, 0f, 0.6f)).rectTransform;
            amount.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-4, -4), new Vector2(84, 44));
            UIKit.Label(amount, "x" + shards, 28, Palette.Primary).rectTransform.Stretch();
            UIKit.Label(c, Loc.UnitName(def), 26, Palette.Text).rectTransform.Band(true, 36, 206, 4);
            UIKit.Label(c, Loc.RarityName(def.rarity), 22, rarity).rectTransform.Band(true, 30, 240, 4);
            var btn = UIKit.ChunkyButton(c, "buy", "", bought ? Palette.Disabled : Palette.Green, null, 30);
            btn.interactable = !bought;
            btn.GetComponent<RectTransform>().Band(false, 88, 8, 10);
            var face = btn.transform.Find("face");
            if (bought) face.GetComponentInChildren<Text>().text = Loc.T("shop.sold");
            else
            {
                Icons.Currency(face, "gold").rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-6, 0), new Vector2(54, 54));
                UIKit.Label(face, gold.ToString(), 32, Palette.Text, TextAnchor.MiddleLeft).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(140, 60));
            }
            if (bought)
            {
                UIKit.Panel(c, "soldShade", new Color(0f, 0f, 0f, 0.45f)).rectTransform.Stretch(0, 0, 0, 96);
                Deco.Check(c, 90).anchoredPosition = new Vector2(0, 40);
            }
            btn.onClick.AddListener(() =>
            {
                var locked = Modal.LockedUnits(App);
                var rewards = meta.BuyDeal(index);
                if (rewards == null) { NotEnough(false); return; }
                Bought(Loc.UnitName(def), rewards, locked);
            });
        }

        void GachaBanner(RectTransform list)
        {
            var cfg = App.MetaConfig;
            var meta = App.Meta;
            var frame = UIKit.Rect("gacha", list).Band(true, 560, _y, 8);
            _y += 584;
            var c = Deco.Frame(frame, "frame", Art.Hex(0xB46BFF), Art.Hex(0x3A1E5E), 9f);
            // Rays + three rare animals as the "prize" showcase
            var rays = UIKit.Icon(c, "rays", Art.Rays, new Color(1f, 0.85f, 1f, 0.35f));
            _raysA = rays.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(760, 760));
            var units = App.Content.Units;
            UnitDef legendary = null, epicA = null, epicB = null;
            foreach (var u in units)
            {
                if (u.rarity == Rarity.Legendary && legendary == null) legendary = u;
                else if (u.rarity == Rarity.Epic) { if (epicA == null) epicA = u; else if (epicB == null) epicB = u; }
            }
            if (epicA != null) Icons.Unit(c, epicA, 4, 200).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-230, -60), new Vector2(200, 200));
            if (epicB != null) Icons.Unit(c, epicB, 4, 200).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(230, -60), new Vector2(200, 200));
            if (legendary != null) Icons.Unit(c, legendary, 7, 280).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -16), new Vector2(280, 280));
            UIKit.Label(c, Loc.T("shop.gacha_text"), 28, Palette.Text).rectTransform.Band(true, 70, 300, 30);

            int left = cfg.gachaPity - meta.State.gachaPity;
            var pity = new Bar(c, Art.Hex(0xFFD23F), 24);
            pity.Root.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 130), new Vector2(700, 44));
            pity.Set(meta.State.gachaPity / (float)cfg.gachaPity, Loc.F("shop.pity", left));

            var one = UIKit.ChunkyButton(c, "pull1", "", Art.Hex(0x3D8BFF), () => Pull(1), 30);
            one.GetComponent<RectTransform>().Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20, 16), new Vector2(390, 104));
            PriceLabel(one, "x1", cfg.gachaPrice);
            var ten = UIKit.ChunkyButton(c, "pull10", "", Art.Hex(0xFFB020), () => Pull(10), 30);
            ten.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20, 16), new Vector2(390, 104));
            PriceLabel(ten, "x10", cfg.gachaPrice10);
            Deco.Tag(ten.GetComponent<RectTransform>(), Loc.T("shop.save_10"), Palette.Red);
            _pulse.Add(ten.GetComponent<RectTransform>());
            var rates = UIKit.ChunkyButton(c, "rates", "?", Palette.PanelDark, ShowRates, 40);
            rates.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 20), new Vector2(96, 96));
        }

        static void PriceLabel(Button b, string count, int gems)
        {
            var face = b.transform.Find("face");
            UIKit.Label(face, count, 38, Palette.Text, TextAnchor.MiddleLeft).rectTransform.Stretch(26, 200, 0, 0);
            Icons.Currency(face, "gems").rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-150, 0), new Vector2(56, 56));
            UIKit.Label(face, gems.ToString(), 34, Palette.Text, TextAnchor.MiddleLeft).rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14, 0), new Vector2(130, 60));
        }

        void ChestTile(RectTransform row, int index, int count, ChestDef def)
        {
            var color = Icons.ChestColor(def.id);
            var cell = Cell(row, index, count);
            var c = Deco.Frame(cell, "frame", color, CardFill);
            var glow = UIKit.Icon(c, "glow", Art.Glow, new Color(color.r, color.g, color.b, 0.6f));
            glow.preserveAspect = false;
            glow.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(300, 220));
            Icons.Chest(c, def.id).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -12), new Vector2(190, 170));
            UIKit.Label(c, Loc.T("chest." + def.id), 30, Palette.Text).rectTransform.Band(true, 40, 186, 6);
            UIKit.Label(c, Loc.F("shop.chest_info", def.shards, def.goldMin), 22, Palette.TextDim).rectTransform.Band(true, 32, 226, 6);
            var btn = UIKit.ChunkyButton(c, "buy", "", Palette.Green, null, 30);
            btn.GetComponent<RectTransform>().Band(false, 88, 8, 10);
            var face = btn.transform.Find("face");
            Icons.Currency(face, "gems").rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-6, 0), new Vector2(54, 54));
            UIKit.Label(face, def.gemPrice.ToString(), 32, Palette.Text, TextAnchor.MiddleLeft).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(140, 60));
            if (def.id == "magic") Deco.Tag(cell, Loc.T("shop.legendary_chance"), Art.Hex(0xB46BFF));
            btn.onClick.AddListener(() =>
                Modal.Confirm(App, Loc.T("chest." + def.id), Loc.F("shop.confirm_gems", def.gemPrice), Loc.F("shop.gems_price", def.gemPrice), () =>
                {
                    var locked = Modal.LockedUnits(App);
                    var rewards = App.Meta.BuyChest(def.id);
                    if (rewards == null) { NotEnough(true); return; }
                    AudioService.Play(Sfx.Chest);
                    Bought(Loc.T("chest." + def.id), rewards, locked, true, def.id);
                }));
        }

        // ------------------------------------------------------------------ actions

        void Bought(string title, List<Reward> rewards, ICollection<string> locked, bool dramatic = false, string chest = null)
        {
            App.SaveProfile();
            AudioService.Play(Sfx.Buy);
            Services.Analytics.LogEvent("shop_buy", new Dictionary<string, object> { { "item", title } });
            Rebuild();
            Modal.Rewards(App, title, rewards, locked, dramatic, chest);
        }

        void NotEnough(bool gems)
        {
            AudioService.Play(Sfx.Error);
            App.Toast(Loc.T(gems ? "shop.not_enough_gems" : "shop.not_enough_gold"));
        }

        void ClaimFreeGold()
        {
            if (!App.Meta.ClaimFreeGold(App.Now)) return;
            Bought(Loc.T("shop.free"), new List<Reward> { new Reward(RewardType.Gold, App.MetaConfig.freeDailyGold) }, null);
        }

        void ClaimAdGems()
        {
            Services.Ads.ShowRewarded("shop_free_gems", granted =>
            {
                if (!granted || !App.Meta.ClaimAdGems(App.Now)) return;
                Bought(Loc.T("shop.free"), new List<Reward> { new Reward(RewardType.Gems, App.MetaConfig.adGems) }, null);
            });
        }

        void Pull(int count)
        {
            int price = count >= 10 ? App.MetaConfig.gachaPrice10 : App.MetaConfig.gachaPrice * count;
            Modal.Confirm(App, Loc.T("shop.gacha"), Loc.F("shop.confirm_gems", price), Loc.F("shop.gems_price", price), () =>
            {
                var locked = Modal.LockedUnits(App);
                var rewards = App.Meta.Pull(count);
                if (rewards == null) { NotEnough(true); return; }
                Services.Analytics.LogEvent("gacha_pull", new Dictionary<string, object> { { "count", count } });
                Bought(Loc.T("shop.gacha"), rewards, locked, true);
            });
        }

        void ShowRates()
        {
            var c = App.MetaConfig;
            var box = Modal.Open(App, Loc.T("shop.rates"), 760);
            var lines = new List<string>();
            for (int r = 0; r < 4; r++)
                lines.Add(Loc.F("shop.rate_line", Loc.RarityName((Rarity)r), c.gachaRarityWeights[r], c.gachaShards[r]));
            lines.Add("");
            lines.Add(Loc.F("shop.pity_rule", c.gachaPity));
            UIKit.Label(box, string.Join("\n", lines.ToArray()), 36, Palette.Text, TextAnchor.UpperLeft).rectTransform.Stretch(60, 60, 150, 60);
        }

        void BuyGold(bool big)
        {
            var c = App.MetaConfig;
            int gems = big ? c.goldPackBigGems : c.goldPackSmallGems;
            int gold = big ? c.goldPackBigGold : c.goldPackSmallGold;
            Modal.Confirm(App, Loc.T("shop.gold"), Loc.F("shop.confirm_gems", gems), Loc.F("shop.gems_price", gems), () =>
            {
                if (!App.Meta.BuyGold(big)) { NotEnough(true); return; }
                Bought(Loc.T("shop.gold"), new List<Reward> { new Reward(RewardType.Gold, gold) }, null);
            });
        }

        void BuyGems(GemPack pack)
        {
            Services.Store.Purchase(pack.id, ok =>
            {
                if (!ok || !App.Meta.GrantGemPack(pack.id)) { App.Toast(Loc.T("shop.purchase_failed")); return; }
                Services.Analytics.LogEvent("iap", new Dictionary<string, object> { { "product", pack.id } });
                Bought(Loc.T("shop.gems"), new List<Reward> { new Reward(RewardType.Gems, pack.gems) }, null);
            });
        }
    }
}
