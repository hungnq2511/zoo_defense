using System;
using System.Collections.Generic;

namespace ZooDefense.Core
{
    public enum BundleLimit { Once = 0, Weekly = 1, Daily = 2 }

    /// <summary>A shop bundle. priceGems > 0: bought with gems; otherwise a real-money store product (productId).</summary>
    [Serializable]
    public class BundleDef
    {
        public string id;              // store product id (real money) / internal id (gems)
        public string price;           // display price for real money ("$1.99"); the store's localized price replaces it later
        public int priceGems;
        public BundleLimit limit;
        public int valuePercent;       // "x10 value" badge
        public int minTrophies;        // shown from this many trophies on
        public int color;              // card colour (hex)
        public string icon;            // main icon: chest id, "gems" or "gold"
        public List<Reward> rewards = new List<Reward>();
    }

    /// <summary>Monthly card: gems at once, then a daily gem gift for 30 days (claimed in the shop).</summary>
    [Serializable]
    public class MonthlyCardDef
    {
        public string id = "monthly_card";
        public string price = "$4.99";
        public int instantGems = 300;
        public int dailyGems = 100;
        public int days = 30;
    }

    /// <summary>Gold bought with gems.</summary>
    [Serializable]
    public struct GoldPack
    {
        public int gold, gems;
        public GoldPack(int gold, int gems) { this.gold = gold; this.gems = gems; }
    }

    /// <summary>The shop's catalogue (bundles, monthly card, gold tiers). Prices are tuned against the gem packs.</summary>
    public static class ShopCatalog
    {
        public static readonly MonthlyCardDef MonthlyCard = new MonthlyCardDef();

        public static readonly GoldPack[] GoldPacks =
        {
            new GoldPack(1000, 60),
            new GoldPack(3000, 160),
            new GoldPack(10000, 480),
        };

        public static readonly List<BundleDef> Bundles = new List<BundleDef>
        {
            new BundleDef
            {
                id = "bundle_starter", price = "$1.99", limit = BundleLimit.Once, valuePercent = 1000, color = 0x4CC46B, icon = "magic",
                rewards = { new Reward(RewardType.Gems, 500), new Reward(RewardType.Gold, 5000), new Reward(RewardType.Chest, 1, "magic") },
            },
            new BundleDef
            {
                id = "bundle_weekly", price = "$4.99", limit = BundleLimit.Weekly, valuePercent = 500, color = 0x3D8BFF, icon = "gold",
                rewards = { new Reward(RewardType.Gems, 800), new Reward(RewardType.Gold, 8000), new Reward(RewardType.Chest, 2, "gold") },
            },
            new BundleDef
            {
                id = "bundle_daily_gems", priceGems = 90, limit = BundleLimit.Daily, valuePercent = 200, color = 0xFFB020, icon = "silver",
                rewards = { new Reward(RewardType.Chest, 1, "silver"), new Reward(RewardType.Gold, 1500), new Reward(RewardType.PassXp, 100) },
            },
            new BundleDef
            {
                id = "bundle_champion", price = "$9.99", limit = BundleLimit.Once, valuePercent = 600, minTrophies = 300, color = 0xB46BFF, icon = "magic",
                rewards = { new Reward(RewardType.Gems, 1500), new Reward(RewardType.Gold, 20000), new Reward(RewardType.Chest, 3, "magic") },
            },
            new BundleDef
            {
                id = "bundle_legend", price = "$19.99", limit = BundleLimit.Once, valuePercent = 800, minTrophies = 1000, color = 0xE5484D, icon = "magic",
                rewards = { new Reward(RewardType.Gems, 4000), new Reward(RewardType.Gold, 50000), new Reward(RewardType.Chest, 6, "magic") },
            },
        };

        public static BundleDef Bundle(string id)
        {
            foreach (var b in Bundles) if (b.id == id) return b;
            return null;
        }

        /// <summary>Every real-money product id (for the store / App Store Connect).</summary>
        public static List<string> RealMoneyProducts(MetaConfig cfg)
        {
            var list = new List<string>();
            foreach (var p in cfg.gemPacks) list.Add(p.id);
            foreach (var b in Bundles) if (b.priceGems <= 0) list.Add(b.id);
            list.Add(MonthlyCard.id);
            return list;
        }
    }

    public partial class PlayerState
    {
        /// <summary>Real-money products bought at least once (first gem pack = double gems).</summary>
        public List<string> productsBought = new List<string>();
        /// <summary>Limited bundles: "id@period" (period = day / week index, 0 for once).</summary>
        public List<string> bundleStamps = new List<string>();
        /// <summary>Store transaction ids already granted (a purchase can be delivered twice; keep the last 60).</summary>
        public List<string> orderLedger = new List<string>();
        /// <summary>Monthly card: last day (inclusive) with a daily gift, and the last day it was claimed.</summary>
        public int monthlyUntilDay = -1;
        public int monthlyClaimDay = -1;
    }

    public sealed partial class MetaService
    {
        static string Stamp(BundleDef b, long now)
        {
            int period = b.limit == BundleLimit.Daily ? Day(now) : b.limit == BundleLimit.Weekly ? GameCalendar.WeekIndex(now) : 0;
            return b.id + "@" + period;
        }

        public bool BundleVisible(BundleDef b) { return b != null && State.trophies >= b.minTrophies && !(b.limit == BundleLimit.Once && State.bundleStamps.Contains(Stamp(b, 0))); }

        public bool CanBuyBundle(BundleDef b, long now) { return BundleVisible(b) && !State.bundleStamps.Contains(Stamp(b, now)); }

        /// <summary>Gem bundle: pays and grants. Returns null when not possible (sold out / not enough gems).</summary>
        public List<Reward> BuyBundleWithGems(string id, long now)
        {
            var b = ShopCatalog.Bundle(id);
            if (b == null || b.priceGems <= 0 || !CanBuyBundle(b, now) || State.gems < b.priceGems) return null;
            State.gems -= b.priceGems;
            return GiveBundle(b, now);
        }

        /// <summary>Real-money bundle, after the store confirmed the purchase.</summary>
        /// <param name="force">The store already took the money: grant even if the limit says sold out.</param>
        public List<Reward> GrantBundle(string id, long now, bool force = false)
        {
            var b = ShopCatalog.Bundle(id);
            if (b == null || b.priceGems > 0 || (!force && !CanBuyBundle(b, now))) return null;
            MarkProduct(id);
            return GiveBundle(b, now);
        }

        List<Reward> GiveBundle(BundleDef b, long now)
        {
            State.bundleStamps.Add(Stamp(b, now));
            // Keep the list short: drop daily / weekly stamps of past periods.
            State.bundleStamps.RemoveAll(s =>
            {
                var other = ShopCatalog.Bundle(s.Substring(0, Math.Max(0, s.IndexOf('@'))));
                return other != null && other.limit != BundleLimit.Once && s != Stamp(other, now);
            });
            var log = new List<Reward>();
            foreach (var r in b.rewards) Grant(r, log);
            return Merge(log);
        }

        void MarkProduct(string id) { if (!State.productsBought.Contains(id)) State.productsBought.Add(id); }

        /// <summary>True the first time a store transaction id is seen (then remembered).</summary>
        public bool RecordOrder(string transactionId)
        {
            if (string.IsNullOrEmpty(transactionId)) return true;
            if (State.orderLedger.Contains(transactionId)) return false;
            State.orderLedger.Add(transactionId);
            if (State.orderLedger.Count > 60) State.orderLedger.RemoveAt(0);
            return true;
        }

        /// <summary>Has the player ever paid real money (no forced ads for them).</summary>
        public bool IsPayer { get { return State.productsBought.Count > 0; } }

        /// <summary>
        /// Grants a real-money product the store has charged for. Returns the rewards (empty when this transaction
        /// was already granted, null for an unknown product id).
        /// </summary>
        public List<Reward> FulfillProduct(string productId, string transactionId, long now)
        {
            if (!RecordOrder(transactionId)) return new List<Reward>();
            int gems = GrantGemPackGems(productId);
            if (gems > 0) return new List<Reward> { new Reward(RewardType.Gems, gems) };
            if (productId == ShopCatalog.MonthlyCard.id) return new List<Reward> { new Reward(RewardType.Gems, GrantMonthlyCard(now)) };
            var b = ShopCatalog.Bundle(productId);
            if (b != null && b.priceGems <= 0) return GrantBundle(productId, now, true);
            if (!string.IsNullOrEmpty(transactionId)) State.orderLedger.Remove(transactionId);
            return null;
        }

        /// <summary>First purchase of each gem pack gives double gems.</summary>
        public bool IsFirstGemPurchase(string productId) { return !State.productsBought.Contains(productId); }

        /// <summary>Gems granted for a gem pack purchase (0 = unknown product). Doubles the first time.</summary>
        public int GrantGemPackGems(string productId)
        {
            foreach (var p in Config.gemPacks)
            {
                if (p.id != productId) continue;
                int gems = IsFirstGemPurchase(productId) ? p.gems * 2 : p.gems;
                State.gems += gems;
                MarkProduct(productId);
                return gems;
            }
            return 0;
        }

        public bool BuyGoldPack(int tier)
        {
            if (tier < 0 || tier >= ShopCatalog.GoldPacks.Length) return false;
            var p = ShopCatalog.GoldPacks[tier];
            if (State.gems < p.gems) return false;
            State.gems -= p.gems;
            State.gold += p.gold;
            return true;
        }

        // ------------------------------------------------------------------ monthly card

        public bool MonthlyActive(long now) { return State.monthlyUntilDay >= Day(now); }
        public int MonthlyDaysLeft(long now) { return Math.Max(0, State.monthlyUntilDay - Day(now) + 1); }
        public bool CanClaimMonthly(long now) { return MonthlyActive(now) && State.monthlyClaimDay != Day(now); }

        /// <summary>After the store confirmed the purchase: instant gems, and 30 more days of daily gems (stacks).</summary>
        public int GrantMonthlyCard(long now)
        {
            var m = ShopCatalog.MonthlyCard;
            int today = Day(now);
            int start = Math.Max(today, State.monthlyUntilDay + 1);
            State.monthlyUntilDay = start + m.days - 1;
            State.gems += m.instantGems;
            MarkProduct(m.id);
            return m.instantGems;
        }

        public bool ClaimMonthly(long now)
        {
            if (!CanClaimMonthly(now)) return false;
            State.monthlyClaimDay = Day(now);
            State.gems += ShopCatalog.MonthlyCard.dailyGems;
            return true;
        }
    }
}
