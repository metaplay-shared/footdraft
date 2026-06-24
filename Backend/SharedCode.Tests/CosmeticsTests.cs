// FOOTDRAFT — cosmetics catalog invariants, incl. the prestige (earn-only) items granted on title wins. Guards
// against the hand-typed grant ids in PlayerActions drifting from the catalog.

using System.Linq;
using NUnit.Framework;

namespace Game.Logic.Tests
{
    [TestFixture]
    public class CosmeticsTests
    {
        // Ids granted in PlayerActions when a title is won — must exist + be exclusive (earn-only).
        static readonly string[] PrestigeIds = { "avatar_wc_champion", "avatar_cup_king", "avatar_invincible" };

        // Ids referenced by Featured Offer bundles (GlobalConfig.Bundles) — must exist + be purchasable (not exclusive).
        static readonly string[] BundleCosmeticIds = { "avatar_fox", "avatar_goat" };

        [Test]
        public void CatalogIdsAreUnique()
        {
            int distinct = CosmeticContent.Items.Select(c => c.Id).Distinct().Count();
            Assert.That(distinct, Is.EqualTo(CosmeticContent.Items.Length), "duplicate cosmetic id");
        }

        [Test]
        public void PrestigeGrantsResolveAndAreExclusive()
        {
            foreach (string id in PrestigeIds)
            {
                CosmeticItem c = CosmeticContent.Items.FirstOrDefault(x => x.Id == id);
                Assert.That(c, Is.Not.Null, $"prestige grant '{id}' must exist in the catalog");
                Assert.That(c.Exclusive, Is.True, $"{id} should be earn-only");
                Assert.That(c.Kind, Is.EqualTo(CosmeticKind.Avatar));
            }
        }

        [Test]
        public void BundleCosmeticsResolveAndAreNotExclusive()
        {
            foreach (string id in BundleCosmeticIds)
            {
                CosmeticItem c = CosmeticContent.Items.FirstOrDefault(x => x.Id == id);
                Assert.That(c, Is.Not.Null, $"bundle cosmetic '{id}' must exist");
                Assert.That(c.Exclusive, Is.False, $"{id} is sold/granted, not a prestige item");
            }
        }

        [Test]
        public void DefaultsAreFreeAndNonExclusive()
        {
            foreach (string id in new[] { "avatar_default", "dice_default" })
            {
                CosmeticItem c = CosmeticContent.Items.First(x => x.Id == id);
                Assert.That(c.GemCost, Is.EqualTo(0));
                Assert.That(c.Exclusive, Is.False);
            }
        }
    }
}
