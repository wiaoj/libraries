using Wiaoj.Webhooks.Publishing.Internal;
using Xunit;

namespace Wiaoj.Webhooks.Publishing.Tests.Unit.Matching;

[Trait("Category", "Unit")]
[Trait("Feature", "Publishing")]
[Trait("Component", "TopicMatcher")]
public sealed class WildcardTopicMatcherTests {
    private readonly WildcardTopicMatcher _matcher = new();

    public sealed class TheUniversalWildcard {
        [Theory]
        [InlineData("order.created")]
        [InlineData("payment.captured")]
        [InlineData("anything.at.all")]
        public void Matches_ReturnsTrue_ForUniversalWildcard_AgainstAnyEvent(string eventName) {
            WildcardTopicMatcher matcher = new();
            Assert.True(matcher.Matches("*", eventName));
        }
    }

    public sealed class TheExactMatching {
        [Fact]
        public void Matches_ReturnsTrue_WhenExactMatch() {
            WildcardTopicMatcher matcher = new();
            Assert.True(matcher.Matches("order.created", "order.created"));
        }

        [Fact]
        public void Matches_IsCaseInsensitive() {
            WildcardTopicMatcher matcher = new();
            Assert.True(matcher.Matches("ORDER.CREATED", "order.created"));
            Assert.True(matcher.Matches("order.created", "ORDER.CREATED"));
        }

        [Fact]
        public void Matches_ReturnsFalse_WhenDifferent() {
            WildcardTopicMatcher matcher = new();
            Assert.False(matcher.Matches("order.created", "order.paid"));
        }
    }

    public sealed class ThePrefixAndSuffixMatching {
        [Theory]
        [InlineData("order.*", "order.created", true)]
        [InlineData("order.*", "order.paid", true)]
        [InlineData("order.*", "payment.captured", false)]
        [InlineData("*.created", "order.created", true)]
        [InlineData("*.created", "invoice.created", true)]
        [InlineData("*.created", "order.paid", false)]
        public void Matches_EvaluatesWildcardCorrectly(string pattern, string eventName, bool expected) {
            WildcardTopicMatcher matcher = new();
            Assert.Equal(expected, matcher.Matches(pattern, eventName));
        }
    }

    public sealed class TheGuardClauses {
        [Theory]
        [InlineData(null, "order.created")]
        [InlineData("", "order.created")]
        [InlineData("   ", "order.created")]
        [InlineData("order.created", null)]
        [InlineData("order.created", "")]
        [InlineData("order.created", "   ")]
        public void Matches_Throws_WhenPatternOrEventNameIsInvalid(string? pattern, string? eventName) {
            WildcardTopicMatcher matcher = new();
            Assert.ThrowsAny<ArgumentException>(() => matcher.Matches(pattern!, eventName!));
        }
    }
}