using FluentAssertions;
using RedisClone.CLI.Commands;
using RedisClone.CLI.Models;
using System.Text;

namespace RedisClone.CLI.Tests.Models;

public sealed class RedisValueTests
{
    private static string Decode(RedisValue v) => Encoding.UTF8.GetString(v.Value);

    [Fact]
    public void Ok_ReturnsSimpleOkString()
    {
        Decode(RedisValue.Ok).Should().Be("+OK\r\n");
    }

    [Fact]
    public void ToError_FormatsCorrectly()
    {
        var result = RedisValue.ToError("ERR something wrong");
        Decode(result).Should().Be("-ERR something wrong\r\n");
        result.Type.Should().Be(RedisType.ErrorString);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public void ToSimpleString_FormatsCorrectly()
    {
        var result = RedisValue.ToSimpleString("PONG");
        Decode(result).Should().Be("+PONG\r\n");
        result.Type.Should().Be(RedisType.SimpleString);
    }

    [Fact]
    public void ToBulkString_WithValue_FormatsCorrectly()
    {
        var result = RedisValue.ToBulkString("hello");
        Decode(result).Should().Be("$5\r\nhello\r\n");
    }

    [Fact]
    public void ToBulkString_Null_ReturnsNilPayload()
    {
        var result = RedisValue.ToBulkString(null);
        Decode(result).Should().Be("$-1\r\n");
    }

    [Fact]
    public void ToBulkStringArray_FormatsCorrectly()
    {
        var result = RedisValue.ToBulkStringArray(["hello", "world"]);
        Decode(result).Should().Be("*2\r\n$5\r\nhello\r\n$5\r\nworld\r\n");
    }

    [Fact]
    public void ToBulkStringArray_EmptyList_FormatsCorrectly()
    {
        var result = RedisValue.ToBulkStringArray([]);
        Decode(result).Should().Be("*0\r\n");
    }

    [Fact]
    public void ToBulkStringArray_FromCommand_IncludesTypeAndArgs()
    {
        var command = new Command(CommandType.Set, ["key", "value"]);
        var result = RedisValue.ToBulkStringArray(command);
        Decode(result).Should().Be("*3\r\n$3\r\nSet\r\n$3\r\nkey\r\n$5\r\nvalue\r\n");
    }

    [Fact]
    public void ToIntegerValue_FormatsCorrectly()
    {
        var result = RedisValue.ToIntegerValue(42);
        Decode(result).Should().Be(":42\r\n");
    }

    [Fact]
    public void ToIntegerValue_Zero()
    {
        Decode(RedisValue.ToIntegerValue(0)).Should().Be(":0\r\n");
    }

    [Fact]
    public void ToIntegerValue_Negative()
    {
        Decode(RedisValue.ToIntegerValue(-1)).Should().Be(":-1\r\n");
    }

    [Fact]
    public void ToBinaryContent_PrefixesWithLength()
    {
        byte[] data = [0x01, 0x02, 0x03];
        var result = RedisValue.ToBinaryContent(data);

        result.Type.Should().Be(RedisType.BinaryContent);
        // Should start with "$3\r\n" followed by the raw bytes
        var str = Decode(result);
        str.Should().StartWith("$3\r\n");
    }

    [Fact]
    public void FromArray_CombinesMultipleValues()
    {
        var values = new[]
        {
            RedisValue.ToBulkStringArray(["subscribe", "ch1", "1"]),
            RedisValue.ToBulkStringArray(["subscribe", "ch2", "2"]),
        };

        var result = RedisValue.FromArray(values);
        var decoded = Decode(result);

        decoded.Should().StartWith("*2\r\n");
        result.Type.Should().Be(RedisType.BulkStringArray);
    }

    [Fact]
    public void EmptyBulkStringArray_IsCorrect()
    {
        Decode(RedisValue.EmptyBulkStringArray).Should().Be("$*0\r\n");
    }

    [Fact]
    public void NullBulkStringArray_IsCorrect()
    {
        Decode(RedisValue.NullBulkStringArray).Should().Be("$-1\r\n");
    }

    [Fact]
    public void UnknownCommandError_IsErrorType()
    {
        RedisValue.UnknownCommandError.Type.Should().Be(RedisType.ErrorString);
        RedisValue.UnknownCommandError.Success.Should().BeFalse();
    }
}