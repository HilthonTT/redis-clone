using FluentAssertions;
using RedisClone.CLI.Storage;

namespace RedisClone.CLI.Tests.Storage;

public sealed class StorageEntryTests
{
    [Fact]
    public void Permanent_NeverExpires()
    {
        var entry = StorageEntry.Permanent("value");

        entry.Value.Should().Be("value");
        entry.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void WithExpiry_LargeMs_NotExpiredImmediately()
    {
        var entry = StorageEntry.WithExpiry("value", 60_000);

        entry.Value.Should().Be("value");
        entry.IsExpired.Should().BeFalse();
    }

    [Fact]
    public async Task WithExpiry_SmallMs_ExpiresAfterDelay()
    {
        var entry = StorageEntry.WithExpiry("value", 50);

        entry.IsExpired.Should().BeFalse();
        await Task.Delay(100);
        entry.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void WithExpiry_ZeroMs_ExpiresImmediately()
    {
        // 0ms expiry should expire almost instantly
        var entry = StorageEntry.WithExpiry("value", 0);

        // Give a tiny margin — but 0ms means it expires at creation time
        entry.IsExpired.Should().BeTrue();
    }
}