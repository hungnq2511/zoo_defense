using NUnit.Framework;
using ZooDefense.Core;
using ZooDefense.Game;

namespace ZooDefense.Tests
{
    public class LocalizationTests
    {
        [Test]
        public void Vietnamese_HasEveryEnglishKey()
        {
            var missing = Loc.MissingKeys(Language.Vietnamese);
            Assert.AreEqual(0, missing.Count, "Missing: " + string.Join(", ", missing.ToArray()));
        }

        [Test]
        public void Vietnamese_HasEveryContentName()
        {
            var content = DefaultContent.CreateDatabase();
            Loc.Init("vi");
            try
            {
                foreach (var u in content.Units) Assert.AreNotEqual(u.displayName, Loc.UnitName(u), "unit." + u.id);
                foreach (var s in content.Sends) Assert.AreNotEqual(s.displayName, Loc.SendName(s), "send." + s.id);
                foreach (var e in content.Enemies) Assert.IsTrue(Loc.EnemyName(e).Length > 0, "enemy." + e.id);
            }
            finally { Loc.Init("en"); }
        }

        [Test]
        public void Translations_KeepTheSamePlaceholders()
        {
            foreach (var lang in Loc.Supported)
            {
                Loc.Init("en");
                var en = LocTables.English();
                var other = lang == Language.Vietnamese ? LocTables.Vietnamese() : LocTables.English();
                foreach (var kv in en)
                {
                    string translated;
                    if (!other.TryGetValue(kv.Key, out translated)) continue;
                    for (int i = 0; i < 3; i++)
                    {
                        string token = "{" + i + "}";
                        Assert.AreEqual(kv.Value.Contains(token), translated.Contains(token), lang + " " + kv.Key + " " + token);
                    }
                }
            }
        }
    }
}
