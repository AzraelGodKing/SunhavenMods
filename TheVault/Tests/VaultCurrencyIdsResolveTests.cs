using NUnit.Framework;
using TheVault.Vault;

namespace TheVault.Tests
{
    [TestFixture]
    public class VaultCurrencyIdsResolveTests
    {
        [TestCase("seasonal_Spring", CurrencyIdKind.Seasonal, "Spring")]
        [TestCase("community_abc", CurrencyIdKind.Community, "abc")]
        [TestCase("special_doubloon", CurrencyIdKind.Special, "doubloon")]
        [TestCase("key_copper", CurrencyIdKind.Key, "copper")]
        [TestCase("ticket_red", CurrencyIdKind.Ticket, "red")]
        [TestCase("orb_blue", CurrencyIdKind.Orb, "blue")]
        [TestCase("custom_my_coin", CurrencyIdKind.Custom, "my_coin")]
        public void Resolve_SplitsKnownPrefixes(string fullId, CurrencyIdKind kind, string shortId)
        {
            var resolved = VaultCurrencyIds.Resolve(fullId);

            Assert.That(resolved.Kind, Is.EqualTo(kind));
            Assert.That(resolved.ShortId, Is.EqualTo(shortId));
        }

        [Test]
        public void Resolve_ParsesSeasonalToken()
        {
            Assert.That(VaultCurrencyIds.Resolve(VaultCurrencyIds.SeasonalWinter).SeasonalToken,
                Is.EqualTo(SeasonalTokenType.Winter));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("seasonal_NotASeason")]
        [TestCase("unknown_thing")]
        [TestCase("Key_copper")]
        public void Resolve_ReturnsNoneForUnknownIds(string fullId)
        {
            Assert.That(VaultCurrencyIds.Resolve(fullId).Kind, Is.EqualTo(CurrencyIdKind.None));
        }

        [Test]
        public void Resolve_ReturnsSameResultWhenCached()
        {
            var first = VaultCurrencyIds.Resolve("key_iron");
            var second = VaultCurrencyIds.Resolve("key_iron");

            Assert.That(second.Kind, Is.EqualTo(first.Kind));
            Assert.That(second.ShortId, Is.SameAs(first.ShortId));
        }
    }
}
