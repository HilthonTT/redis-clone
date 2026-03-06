namespace RedisClone.CLI.Extensions;

internal static class StringExtensions
{
    internal static void WriteLineEncoded(this string str)
    {
        Console.WriteLine(str.Replace("\r\n", "\\r\\n"));
    }
}
