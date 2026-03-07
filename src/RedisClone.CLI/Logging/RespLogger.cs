using System.Text;

namespace RedisClone.CLI.Logging;

internal static class RespLogger
{
    public static void Waiting(int connectionId) =>
        Console.WriteLine($"[{connectionId}] Waiting for request...");

    public static void Received(int connectionId, string payload) =>
        Console.WriteLine($"[{connectionId}] Received: {Escape(payload)}");

    public static void Sending(int connectionId, byte[] payload) =>
        Console.WriteLine($"[{connectionId}] Sending: {Escape(Encoding.UTF8.GetString(payload))}");

    public static void Disconnected(int connectionId) =>
        Console.WriteLine($"[{connectionId}] Client disconnected.");

    private static string Escape(string value) =>
        value.Replace("\r\n", "\\r\\n");
}
