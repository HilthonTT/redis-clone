namespace RedisClone.CLI.Helpers;

internal static class StringHelpers
{
    private const string AlphanumericChars = "abcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>
    /// Generates a cryptographically non-sensitive random alphanumeric string
    /// of the specified length using the shared Random instance.
    /// </summary>
    internal static string GenerateRandomString(int length)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be positive.");
        }

        var chars = Random.Shared.GetItems<char>(AlphanumericChars, length);
        return new string(chars);
    }
}