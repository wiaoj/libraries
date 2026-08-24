using Wiaoj.Webhooks.Publishing.Internal;

namespace Wiaoj.Webhooks.Publishing.Tests.Unit.Matching;

[Trait("Category", "Unit")]
[Trait("Feature", "Gateway")]
[Trait("Component", "SubscriptionMatcher")]
public sealed class WildcardSubscriptionMatcherTests {
    private readonly WildcardSubscriptionMatcher _matcher = new();

    public sealed class UniversalWildcard {
        [Theory]
        [InlineData("order.created")]
        [InlineData("payment.captured")]
        [InlineData("user.deleted")]
        [InlineData("anything.at.all")]
        public void Matches_ReturnsTrue_ForUniversalWildcard_AgainstAnyEvent(string eventName) {
            WildcardSubscriptionMatcher matcher = new();
            Assert.True(matcher.Matches("*", eventName));
        }
    }

    public sealed class ExactMatching {
        [Fact]
        public void Matches_ReturnsTrue_WhenPatternAndEventNameAreExactMatch() {
            WildcardSubscriptionMatcher matcher = new();
            Assert.True(matcher.Matches("order.created", "order.created"));
        }

        [Fact]
        public void Matches_IsCaseInsensitive() {
            WildcardSubscriptionMatcher matcher = new();
            Assert.True(matcher.Matches("ORDER.CREATED", "order.created"));
            Assert.True(matcher.Matches("order.created", "ORDER.CREATED"));
        }

        [Fact]
        public void Matches_ReturnsFalse_WhenEventNamesDiffer() {
            WildcardSubscriptionMatcher matcher = new();
            Assert.False(matcher.Matches("order.created", "order.paid"));
            Assert.False(matcher.Matches("order.created", "invoice.created"));
        }
    }

    public sealed class PrefixWildcardMatching {
        [Theory]
        [InlineData("order.*", "order.created", true)]
        [InlineData("order.*", "order.paid", true)]
        [InlineData("order.*", "order.cancelled.refunded", true)]
        [InlineData("order.*", "payment.captured", false)]
        [InlineData("order.*", "preorder.created", false)]
        public void Matches_EvaluatesPrefixWildcardCorrectly(string pattern, string eventName, bool expected) {
            WildcardSubscriptionMatcher matcher = new();
            Assert.Equal(expected, matcher.Matches(pattern, eventName));
        }
    }

    public sealed class SuffixWildcardMatching {
        [Theory]
        [InlineData("*.created", "order.created", true)]
        [InlineData("*.created", "invoice.created", true)]
        [InlineData("*.created", "user.created", true)]
        [InlineData("*.created", "order.paid", false)]
        [InlineData("*.created", "order.created.v2", false)]
        public void Matches_EvaluatesSuffixWildcardCorrectly(string pattern, string eventName, bool expected) {
            WildcardSubscriptionMatcher matcher = new();
            Assert.Equal(expected, matcher.Matches(pattern, eventName));
        }
    }

    public sealed class GuardClauses {
        [Theory]
        [InlineData(null, "order.created")]
        [InlineData("", "order.created")]
        [InlineData("   ", "order.created")]
        [InlineData("order.created", null)]
        [InlineData("order.created", "")]
        [InlineData("order.created", "   ")]
        public void Matches_Throws_WhenPatternOrEventNameIsInvalid(string? pattern, string? eventName) {
            WildcardSubscriptionMatcher matcher = new();
            Assert.ThrowsAny<ArgumentException>(() => matcher.Matches(pattern!, eventName!));
        }
    }
}