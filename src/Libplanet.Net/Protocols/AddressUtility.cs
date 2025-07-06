using Libplanet.Types;

namespace Libplanet.Net.Protocols;

public static class AddressUtility
{
    public static Address GetDifference(Address left, Address right)
    {
        var bytes = new byte[Address.Size];
        var bytes1 = left.Bytes;
        var bytes2 = right.Bytes;

        for (var i = 0; i < Address.Size; i++)
        {
            bytes[i] = (byte)(bytes1[i] ^ bytes2[i]);
        }

        return new Address(bytes);
    }

    public static int CommonPrefixLength(Address left, Address right)
    {
        var bytes = GetDifference(left, right).Bytes;
        var length = 0;

        foreach (byte @byte in bytes)
        {
            var mask = 1 << 7;
            while (mask != 0)
            {
                if ((mask & @byte) != 0)
                {
                    return length;
                }

                length++;
                mask >>= 1;
            }
        }

        return length;
    }

    public static int GetDistance(Address left, Address right) => (Address.Size * 8) - CommonPrefixLength(left, right);
}
