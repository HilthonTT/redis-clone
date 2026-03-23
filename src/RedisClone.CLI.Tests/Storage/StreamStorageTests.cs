using FluentAssertions;
using RedisClone.CLI.Storage;

namespace RedisClone.CLI.Tests.Storage;

public sealed class StreamStorageTests
{
    private readonly StreamStorage _storage = new();

    [Fact]
    public void TryAppend_WithExplicitId_Succeeds()
    {
        bool result = _storage.TryAppend("stream1", "1-0",
            new Dictionary<string, string> { ["name"] = "hans" },
            out var id, out var error);

        result.Should().BeTrue();
        id.Should().Be("1-0");
        error.Should().BeNull();
    }

    [Fact]
    public void TryAppend_WithAutoSequence_Succeeds()
    {
        _storage.TryAppend("stream1", "1-0",
            new Dictionary<string, string> { ["k"] = "v1" },
            out _, out _);

        bool result = _storage.TryAppend("stream1", "1-*",
            new Dictionary<string, string> { ["k"] = "v2" },
            out var id, out var error);

        result.Should().BeTrue();
        id.Should().Be("1-1");
        error.Should().BeNull();
    }

    [Fact]
    public void TryAppend_WithFullAutoId_Succeeds()
    {
        bool result = _storage.TryAppend("stream1", "*",
            new Dictionary<string, string> { ["k"] = "v" },
            out var id, out var error);

        result.Should().BeTrue();
        id.Should().NotBeNullOrEmpty();
        error.Should().BeNull();
    }

    [Fact]
    public void TryAppend_ZeroZeroId_Fails()
    {
        bool result = _storage.TryAppend("stream1", "0-0",
            new Dictionary<string, string> { ["k"] = "v" },
            out var id, out var error);

        result.Should().BeFalse();
        id.Should().BeNull();
        error.Should().Contain("greater than 0-0");
    }

    [Fact]
    public void TryAppend_SmallerIdThanExisting_Fails()
    {
        _storage.TryAppend("stream1", "5-0",
            new Dictionary<string, string> { ["k"] = "v1" },
            out _, out _);

        bool result = _storage.TryAppend("stream1", "3-0",
            new Dictionary<string, string> { ["k"] = "v2" },
            out var id, out var error);

        result.Should().BeFalse();
        id.Should().BeNull();
        error.Should().Contain("equal or smaller");
    }

    [Fact]
    public void TryAppend_EqualIdToExisting_Fails()
    {
        _storage.TryAppend("stream1", "5-0",
            new Dictionary<string, string> { ["k"] = "v1" },
            out _, out _);

        bool result = _storage.TryAppend("stream1", "5-0",
            new Dictionary<string, string> { ["k"] = "v2" },
            out _, out var error);

        result.Should().BeFalse();
        error.Should().Contain("equal or smaller");
    }

    [Fact]
    public void TryAppend_InvalidTimestamp_Fails()
    {
        bool result = _storage.TryAppend("stream1", "abc-0",
            new Dictionary<string, string> { ["k"] = "v" },
            out _, out var error);

        result.Should().BeFalse();
        error.Should().Contain("Invalid stream ID");
    }

    [Fact]
    public void TryAppend_InvalidSequence_Fails()
    {
        bool result = _storage.TryAppend("stream1", "1-abc",
            new Dictionary<string, string> { ["k"] = "v" },
            out _, out var error);

        result.Should().BeFalse();
        error.Should().Contain("Invalid stream ID");
    }

    [Fact]
    public void TryAppend_IncreasingTimestamps_AllSucceed()
    {
        _storage.TryAppend("s", "1-0", new() { ["a"] = "1" }, out _, out _).Should().BeTrue();
        _storage.TryAppend("s", "2-0", new() { ["a"] = "2" }, out _, out _).Should().BeTrue();
        _storage.TryAppend("s", "3-0", new() { ["a"] = "3" }, out _, out _).Should().BeTrue();
    }

    [Fact]
    public void TryAppend_SameTimestampIncreasingSequence_Succeeds()
    {
        _storage.TryAppend("s", "1-0", new() { ["a"] = "1" }, out _, out _).Should().BeTrue();
        _storage.TryAppend("s", "1-1", new() { ["a"] = "2" }, out _, out _).Should().BeTrue();
        _storage.TryAppend("s", "1-2", new() { ["a"] = "3" }, out _, out _).Should().BeTrue();
    }

    [Fact]
    public void HasKey_AfterAppend_ReturnsTrue()
    {
        _storage.TryAppend("stream1", "1-0",
            new Dictionary<string, string> { ["k"] = "v" },
            out _, out _);

        _storage.HasKey("stream1").Should().BeTrue();
    }

    [Fact]
    public void HasKey_NoAppend_ReturnsFalse()
    {
        _storage.HasKey("nonexistent").Should().BeFalse();
    }

    [Fact]
    public void TryAppend_DifferentStreams_AreIndependent()
    {
        _storage.TryAppend("s1", "5-0", new() { ["k"] = "v" }, out _, out _);

        // A lower ID should succeed on a different stream
        _storage.TryAppend("s2", "1-0", new() { ["k"] = "v" }, out _, out _)
            .Should().BeTrue();
    }
}
