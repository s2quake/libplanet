using System.Text;

namespace Libplanet.RocksDBStore;

internal static class RocksDBStoreBitConverter
{
    public static long ToInt64(byte[] value)
    {
        byte[] bytes = new byte[sizeof(long)];
        value.CopyTo(bytes, 0);

        // Use Big-endian to order index lexicographically.
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return BitConverter.ToInt64(bytes, 0);
    }

    public static int ToInt32(byte[] value)
    {
        byte[] bytes = new byte[sizeof(int)];
        value.CopyTo(bytes, 0);

        // Use Big-endian to order index lexicographically.
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return BitConverter.ToInt32(bytes, 0);
    }

    public static string GetString(byte[] value)
    {
        return Encoding.UTF8.GetString(value);
    }

    public static byte[] GetBytes(long value)
    {
        byte[] bytes = BitConverter.GetBytes(value);

        // Use Big-endian to order index lexicographically.
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return bytes;
    }

    public static byte[] GetBytes(string value)
    {
        return Encoding.UTF8.GetBytes(value);
    }
}
