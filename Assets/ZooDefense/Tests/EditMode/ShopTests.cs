using NUnit.Framework;
using ZooDefense.Core;

namespace ZooDefense.Tests
{
    /// <summary>Shop bundles, monthly card, first-purchase bonus.</summary>
    public class ShopTests
    {
        const long Day0 = 1767225600 + 86400 * 3;

        static MetaService NewService()
        {
            var cfg = DefaultMeta.Create();
            var content = DefaultContent.CreateDatabase();
            var meta = new MetaService(cfg, content, MetaService.NewPlayer(cfg, content, 7));
            meta.Refresh(Day0);
            return meta;
        }

        [Test]
        public void StarterPack_OnlyOnce()
        {
            var meta = NewService();
            int gems = meta.State.gems;
            Assert.IsNotNull(meta.GrantBundle("bundle_starter", Day0));
            Assert.Greater(meta.State.gems, gems + 400);
            Assert.IsNull(meta.GrantBundle("bundle_starter", Day0 + 86400 * 30), "one-time");
            Assert.IsFalse(meta.BundleVisible(ShopCatalog.Bundle("bundle_starter")));
        }

        [Test]
        public void DailyGemBundle_ResetsNextDay()
        {
            var meta = NewService();
            meta.State.gems = 1000;
            Assert.IsNotNull(meta.BuyBundleWithGems("bundle_daily_gems", Day0));
            Assert.IsNull(meta.BuyBundleWithGems("bundle_daily_gems", Day0 + 60));
            Assert.IsNotNull(meta.BuyBundleWithGems("bundle_daily_gems", Day0 + 86400));
            Assert.AreEqual(1000 - 2 * ShopCatalog.Bundle("bundle_daily_gems").priceGems, meta.State.gems);
            Assert.Less(meta.State.bundleStamps.Count, 3, "old stamps are dropped");
        }

        [Test]
        public void HighBundles_NeedTrophies()
        {
            var meta = NewService();
            meta.State.trophies = 0;
            Assert.IsNull(meta.GrantBundle("bundle_legend", Day0));
            meta.State.trophies = 1200;
            Assert.IsNotNull(meta.GrantBundle("bundle_legend", Day0));
        }

        [Test]
        public void FirstGemPack_IsDoubled()
        {
            var meta = NewService();
            int g0 = meta.State.gems;
            Assert.AreEqual(160, meta.GrantGemPackGems("gems_small"));
            Assert.AreEqual(80, meta.GrantGemPackGems("gems_small"));
            Assert.AreEqual(g0 + 240, meta.State.gems);
            Assert.AreEqual(0, meta.GrantGemPackGems("nope"));
        }

        [Test]
        public void MonthlyCard_DailyGemsFor30Days_AndStacks()
        {
            var meta = NewService();
            var m = ShopCatalog.MonthlyCard;
            int g0 = meta.State.gems;
            meta.GrantMonthlyCard(Day0);
            Assert.AreEqual(g0 + m.instantGems, meta.State.gems);
            Assert.IsTrue(meta.ClaimMonthly(Day0));
            Assert.IsFalse(meta.ClaimMonthly(Day0 + 100), "once a day");
            Assert.IsTrue(meta.ClaimMonthly(Day0 + 86400 * 29));
            Assert.IsFalse(meta.CanClaimMonthly(Day0 + 86400 * 30), "expired after 30 days");
            meta.GrantMonthlyCard(Day0 + 86400 * 10);
            Assert.AreEqual(50, meta.MonthlyDaysLeft(Day0 + 86400 * 10), "a second card adds 30 days");
        }

        [Test]
        public void StorePurchase_SameTransactionIsGrantedOnce()
        {
            var meta = NewService();
            int g0 = meta.State.gems;
            var first = meta.FulfillProduct("gems_medium", "tx-1", Day0);
            Assert.AreEqual(1000, first[0].amount, "first purchase doubled");
            Assert.AreEqual(0, meta.FulfillProduct("gems_medium", "tx-1", Day0).Count, "re-delivered: nothing more");
            Assert.AreEqual(g0 + 1000, meta.State.gems);
            Assert.IsNull(meta.FulfillProduct("unknown", "tx-2", Day0));
            Assert.IsNotNull(meta.FulfillProduct("monthly_card", "tx-3", Day0));
            Assert.IsTrue(meta.CanClaimMonthly(Day0));
            // A paid weekly pack is granted even if the limit was already used this week.
            Assert.IsNotNull(meta.FulfillProduct("bundle_weekly", "tx-4", Day0));
            Assert.IsNotNull(meta.FulfillProduct("bundle_weekly", "tx-5", Day0));
            Assert.IsTrue(meta.IsPayer);
        }

        [Test]
        public void GoldPacks_CostGems()
        {
            var meta = NewService();
            meta.State.gems = 200; int gold = meta.State.gold;
            Assert.IsTrue(meta.BuyGoldPack(1));
            Assert.AreEqual(gold + ShopCatalog.GoldPacks[1].gold, meta.State.gold);
            Assert.IsFalse(meta.BuyGoldPack(2), "not enough gems");
        }
    }
}
