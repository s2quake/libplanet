namespace Libplanet.Data.Structures;

internal static class StringExtensions
{
    public static string GetCommonPrefix(this string @this, string key)
    {
        var minLength = Math.Min(@this.Length, key.Length);
        var count = 0;

        for (var i = 0; i < minLength; i++)
        {
            if (@this[i] != key[i])
            {
                break;
            }

            count++;
        }

        return @this[0..count];
    }
}
