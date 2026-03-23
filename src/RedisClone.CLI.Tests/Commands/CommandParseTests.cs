using FluentAssertions;
using RedisClone.CLI.Commands;

namespace RedisClone.CLI.Tests.Commands;

public sealed class CommandParseTests
{
    [Fact]
    public void Parse_PingCommand_ReturnsPingType()
    {
        string raw = "*1\r\n$4\r\nPING\r\n";
        var command = Command.Parse(raw);

        command.Type.Should().Be(CommandType.Ping);
        command.Arguments.Should().BeEmpty();
    }

    [Fact]
    public void Parse_SetCommand_ParsesKeyAndValue()
    {
        string raw = "*3\r\n$3\r\nSET\r\n$4\r\nname\r\n$4\r\nhans\r\n";
        var command = Command.Parse(raw);

        command.Type.Should().Be(CommandType.Set);
        command.Arguments.Should().BeEquivalentTo(["name", "hans"], o => o.WithStrictOrdering());
    }

    [Fact]
    public void Parse_CaseInsensitive()
    {
        string raw = "*1\r\n$4\r\nping\r\n";
        var command = Command.Parse(raw);
        command.Type.Should().Be(CommandType.Ping);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsUnknown()
    {
        Command.Parse("").Should().Be(Command.Unknown);
    }

    [Fact]
    public void Parse_Whitespace_ReturnsUnknown()
    {
        Command.Parse("   ").Should().Be(Command.Unknown);
    }

    [Fact]
    public void Parse_TooFewTokens_ReturnsUnknown()
    {
        Command.Parse("*1\r\n$4\r\n").Should().Be(Command.Unknown);
    }

    [Fact]
    public void Parse_UnknownCommandName_ReturnsUnknown()
    {
        string raw = "*1\r\n$7\r\nINVALID\r\n";
        Command.Parse(raw).Type.Should().Be(CommandType.Unknown);
    }

    [Fact]
    public void Parse_GetWithKey_ParsesCorrectly()
    {
        string raw = "*2\r\n$3\r\nGET\r\n$4\r\nname\r\n";
        var command = Command.Parse(raw);

        command.Type.Should().Be(CommandType.Get);
        command.Arguments.Should().Equal(["name"]);
    }

    [Fact]
    public void Parse_SetWithExpiry_ParsesAllArguments()
    {
        string raw = "*5\r\n$3\r\nSET\r\n$3\r\nkey\r\n$5\r\nvalue\r\n$2\r\nPX\r\n$4\r\n5000\r\n";
        var command = Command.Parse(raw);

        command.Type.Should().Be(CommandType.Set);
        command.Arguments.Should().Equal(["key", "value", "PX", "5000"]);
    }

    [Fact]
    public void Parse_EchoCommand()
    {
        string raw = "*2\r\n$4\r\nECHO\r\n$5\r\nhello\r\n";
        var command = Command.Parse(raw);

        command.Type.Should().Be(CommandType.Echo);
        command.Arguments.Should().Equal(["hello"]);
    }

    [Fact]
    public void Parse_LPushMultipleValues()
    {
        string raw = "*4\r\n$5\r\nLPUSH\r\n$6\r\nmylist\r\n$1\r\na\r\n$1\r\nb\r\n";
        var command = Command.Parse(raw);

        command.Type.Should().Be(CommandType.LPush);
        command.Arguments.Should().Equal(["mylist", "a", "b"]);
    }

    [Fact]
    public void Parse_SubscribeMultipleChannels()
    {
        string raw = "*3\r\n$9\r\nSUBSCRIBE\r\n$4\r\nnews\r\n$6\r\nevents\r\n";
        var command = Command.Parse(raw);

        command.Type.Should().Be(CommandType.Subscribe);
        command.Arguments.Should().Equal(["news", "events"]);
    }
}
