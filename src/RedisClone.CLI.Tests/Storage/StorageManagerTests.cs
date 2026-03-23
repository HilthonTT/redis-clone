using FluentAssertions;
using RedisClone.CLI.Storage;

namespace RedisClone.CLI.Tests.Storage;

public sealed class StorageManagerTests : IDisposable
{
    private readonly KvpStorage _kvp = new();
    private readonly ListStorage _list = new();
    private readonly StreamStorage _stream = new();
    private readonly StorageManager _manager;

    public StorageManagerTests()
    {
        _manager = new StorageManager(_kvp, _list, _stream);
    }

    [Fact]
    public void GetType_StringKey_ReturnsString()
    {
        _kvp.Set("name", "hans");
        _manager.GetType("name").Should().Be(ValueType.String);
    }

    [Fact]
    public void GetType_ListKey_ReturnsList()
    {
        _list.AddLast("mylist", ["a"]);
        _manager.GetType("mylist").Should().Be(ValueType.List);
    }

    [Fact]
    public void GetType_StreamKey_ReturnsStream()
    {
        _stream.TryAppend("mystream", "1-0", new() { ["k"] = "v" }, out _, out _);
        _manager.GetType("mystream").Should().Be(ValueType.Stream);
    }

    [Fact]
    public void GetType_MissingKey_ReturnsNone()
    {
        _manager.GetType("missing").Should().Be(ValueType.None);
    }

    [Fact]
    public void GetAllKeys_ReturnsUnionOfAllStores_Sorted()
    {
        _kvp.Set("zeta", "v");
        _list.AddLast("alpha", ["a"]);
        _stream.TryAppend("mid", "1-0", new() { ["k"] = "v" }, out _, out _);

        _manager.GetAllKeys().Should().BeEquivalentTo(
            ["alpha", "mid", "zeta"],
            o => o.WithStrictOrdering());
    }

    [Fact]
    public void GetAllKeys_Empty_ReturnsEmpty()
    {
        _manager.GetAllKeys().Should().BeEmpty();
    }

    public void Dispose() => _kvp.Dispose();
}