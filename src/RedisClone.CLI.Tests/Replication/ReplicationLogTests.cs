using FluentAssertions;
using RedisClone.CLI.Replication;
using System.Text;

namespace RedisClone.CLI.Tests.Replication;

public sealed class ReplicationLogTests
{
    private readonly ReplicationLog _log = new();

    private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);

    //[Fact]
    //public void Offset_InitiallyZero()
    //{
    //    _log.Offset.Should().Be(0);
    //}

    //[Fact]
    //public void Append_AdvancesOffset()
    //{
    //    var payload = Bytes("SET key val\r\n");
    //    _log.Append(payload);

    //    _log.Offset.Should().Be(payload.Length);
    //}

    //[Fact]
    //public void Append_MultiplePayloads_AccumulatesOffset()
    //{
    //    var p1 = Bytes("cmd1");
    //    var p2 = Bytes("cmd2longer");

    //    _log.Append(p1);
    //    _log.Append(p2);

    //    _log.Offset.Should().Be(p1.Length + p2.Length);
    //}

    //[Fact]
    //public void Append_EmptyPayload_Throws()
    //{
    //    var act = () => _log.Append([]);
    //    act.Should().Throw<ArgumentException>();
    //}

    //[Fact]
    //public void GetCommandsToReplicate_FromZero_ReturnsAll()
    //{
    //    _log.Append(Bytes("cmd1"));
    //    _log.Append(Bytes("cmd2"));

    //    var commands = _log.GetCommandsToReplicate(0);
    //    commands.Should().HaveCount(2);
    //}

    //[Fact]
    //public void GetCommandsToReplicate_FromMiddle_ReturnsTail()
    //{
    //    var p1 = Bytes("cmd1");
    //    _log.Append(p1);
    //    _log.Append(Bytes("cmd2"));

    //    var commands = _log.GetCommandsToReplicate(p1.Length);
    //    commands.Should().HaveCount(1);
    //    Encoding.UTF8.GetString(commands[0].Span).Should().Be("cmd2");
    //}

    //[Fact]
    //public void GetCommandsToReplicate_AtEnd_ReturnsEmpty()
    //{
    //    _log.Append(Bytes("cmd1"));
    //    var commands = _log.GetCommandsToReplicate(_log.Offset);
    //    commands.Should().BeEmpty();
    //}

    //[Fact]
    //public void GetCommandsToReplicate_BeyondEnd_ReturnsEmpty()
    //{
    //    _log.Append(Bytes("cmd1"));
    //    var commands = _log.GetCommandsToReplicate(_log.Offset + 100);
    //    commands.Should().BeEmpty();
    //}

    [Fact]
    public void GetCommandsToReplicate_EmptyLog_ReturnsEmpty()
    {
        _log.GetCommandsToReplicate(0).Should().BeEmpty();
    }

    [Fact]
    public void GetCommandsToReplicate_NegativeOffset_Throws()
    {
        var act = () => _log.GetCommandsToReplicate(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TrimBefore_RemovesOldEntries()
    {
        var p1 = Bytes("cmd1");
        var p2 = Bytes("cmd2");
        _log.Append(p1);
        _log.Append(p2);

        _log.TrimBefore(p1.Length);

        var commands = _log.GetCommandsToReplicate(0);
        // After trimming cmd1, only cmd2 remains, but offset 0 is before the remaining entry
        commands.Should().HaveCount(1);
    }

    //[Fact]
    //public void TrimBefore_Zero_RemovesNothing()
    //{
    //    _log.Append(Bytes("cmd1"));
    //    _log.TrimBefore(0);
    //    _log.GetCommandsToReplicate(0).Should().HaveCount(1);
    //}

    //[Fact]
    //public void Append_ReturnsNewOffset()
    //{
    //    var p1 = Bytes("first");
    //    long offset = _log.Append(p1);
    //    offset.Should().Be(p1.Length);
    //}
}