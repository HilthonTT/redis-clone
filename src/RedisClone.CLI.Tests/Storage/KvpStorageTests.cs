using FluentAssertions;
using RedisClone.CLI.Storage;

namespace RedisClone.CLI.Tests.Storage;

public sealed class KvpStorageTests : IDisposable
{
    private readonly KvpStorage _storage = new();

    [Fact]
    public void Get_MissingKey_ReturnsNull()
    {
        _storage.Get("nonexistent").Should().BeNull();
    }

    [Fact]
    public void Set_ThenGet_ReturnsValue()
    {
        _storage.Set("key", "value");
        _storage.Get("key").Should().Be("value");
    }

    [Fact]
    public void Set_OverwritesExistingKey()
    {
        _storage.Set("key", "first");
        _storage.Set("key", "second");
        _storage.Get("key").Should().Be("second");
    }

    [Fact]
    public void Keys_ReturnsAllSetKeys()
    {
        _storage.Set("a", "1");
        _storage.Set("b", "2");
        _storage.Set("c", "3");

        _storage.Keys.Should().BeEquivalentTo(["a", "b", "c"]);
    }

    [Fact]
    public void Remove_ExistingKey_ReturnsTrue()
    {
        _storage.Set("key", "value");
        _storage.Remove("key").Should().BeTrue();
        _storage.Get("key").Should().BeNull();
    }

    [Fact]
    public void Remove_MissingKey_ReturnsFalse()
    {
        _storage.Remove("missing").Should().BeFalse();
    }

    [Fact]
    public async Task Get_ExpiredKey_ReturnsNull()
    {
        _storage.Set("temp", "data", expireAfterMs: 50);
        _storage.Get("temp").Should().Be("data");

        await Task.Delay(100);

        _storage.Get("temp").Should().BeNull();
    }

    [Fact]
    public void Get_NonExpiredKey_ReturnsValue()
    {
        _storage.Set("temp", "data", expireAfterMs: 60_000);
        _storage.Get("temp").Should().Be("data");
    }

    [Fact]
    public void Initialize_LoadsEntries()
    {
        var data = new Dictionary<string, StorageEntry>
        {
            ["loaded1"] = StorageEntry.Permanent("val1"),
            ["loaded2"] = StorageEntry.Permanent("val2"),
        };

        _storage.Initialize(data);

        _storage.Get("loaded1").Should().Be("val1");
        _storage.Get("loaded2").Should().Be("val2");
    }

    [Fact]
    public void Initialize_DoesNotOverwriteExisting()
    {
        _storage.Set("key", "original");

        var data = new Dictionary<string, StorageEntry>
        {
            ["key"] = StorageEntry.Permanent("loaded"),
        };

        _storage.Initialize(data);

        _storage.Get("key").Should().Be("original");
    }

    public void Dispose() => _storage.Dispose();
}
