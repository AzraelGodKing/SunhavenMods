using NUnit.Framework;
using TheVault.Vault;

namespace TheVault.Tests
{
    [TestFixture]
    public class VaultCurrencyIdsTests
    {
        [TestCase(VaultCurrencyIds.SeasonalSpring, VaultCurrencyKind.Seasonal, "Spring")]
        [TestCase("community_event", VaultCurrencyKind.Community, "event")]
        [TestCase(VaultCurrencyIds.KeyCopper, VaultCurrencyKind.Key, "copper")]
        [TestCase(VaultCurrencyIds.SpecialCommunityToken, VaultCurrencyKind.Special, "communitytoken")]
        [TestCase("orb_sun", VaultCurrencyKind.Orb, "sun")]
        [TestCase("custom_mod_shard", VaultCurrencyKind.Custom, "mod_shard")]
        public void TryDispatch_StripsKnownPrefixes(string fullId, VaultCurrencyKind expectedKind, string expectedRemainder)
        {
            Assert.That(VaultCurrencyIds.TryDispatch(fullId, out var kind, out var remainder), Is.True);
            Assert.That(kind, Is.EqualTo(expectedKind));
            Assert.That(remainder, Is.EqualTo(expectedRemainder));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("gold")]
        [TestCase("ticket_pirate")]
        public void TryDispatch_RejectsUnknownOrTicketPrefix(string fullId)
        {
            Assert.That(VaultCurrencyIds.TryDispatch(fullId, out var kind, out var remainder), Is.False);
            Assert.That(kind, Is.EqualTo(default(VaultCurrencyKind)));
            Assert.That(remainder, Is.Null);
        }

        [Test]
        public void CurrencyCategory_KeepsCommunityTokenAndOrbValues()
        {
            Assert.That((int)CurrencyCategory.CommunityToken, Is.EqualTo(1));
            Assert.That((int)CurrencyCategory.Orb, Is.EqualTo(4));
        }
    }
}
