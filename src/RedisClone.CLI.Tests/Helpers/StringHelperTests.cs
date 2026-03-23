using FluentAssertions;
using RedisClone.CLI.Helpers;

namespace RedisClone.CLI.Tests.Helpers;

public sealed class StringHelpersTests
{
    [Fact]
    public void GenerateRandomString_ReturnsCorrectLength()
    {
        string result = StringHelpers.GenerateRandomString(40);
        result.Should().HaveLength(40);
    }

    [Fact]
    public void GenerateRandomString_ContainsOnlyAlphanumeric()
    {
        string result = StringHelpers.GenerateRandomString(100);
        result.Should().MatchRegex("^[a-z0-9]+$");
    }

    [Fact]
    public void GenerateRandomString_ZeroLength_Throws()
    {
        var act = () => StringHelpers.GenerateRandomString(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GenerateRandomString_NegativeLength_Throws()
    {
        var act = () => StringHelpers.GenerateRandomString(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GenerateRandomString_TwoCallsProduceDifferentResults()
    {
        // Extremely unlikely to be equal for length 40
        string a = StringHelpers.GenerateRandomString(40);
        string b = StringHelpers.GenerateRandomString(40);
        a.Should().NotBe(b);
    }
}
