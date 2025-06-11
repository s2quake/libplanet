using Libplanet.Types;

namespace Libplanet.Net.Protocols;

public static class Kademlia
{
    public const int BucketSize = 16;

    public const int TableSize = Address.Size * 8;

    public const int FindConcurrency = 3;

    public const int MaxDepth = 3;

    public static Address CalculateDifference(Address left, Address right)
    {
        byte[] dba = Enumerable.Zip(
            left.Bytes, right.Bytes, (l, r) => (byte)(l ^ r)).ToArray();
        return new Address([.. dba]);
    }

    public static int CommonPrefixLength(Address left, Address right)
    {
        ImmutableArray<byte> bytes = CalculateDifference(left, right).Bytes;
        int length = 0;

        foreach (byte b in bytes)
        {
            int mask = 1 << 7;
            while (mask != 0)
            {
                if ((mask & b) != 0)
                {
                    return length;
                }

                length++;
                mask >>= 1;
            }
        }

        return length;
    }

    public static int CalculateDistance(Address left, Address right)
    {
        return (Address.Size * 8) - CommonPrefixLength(left, right);
    }

    public static IEnumerable<BoundPeer> SortByDistance(
        IEnumerable<BoundPeer> peers,
        Address target)
    {
        return peers.OrderBy(peer => CalculateDistance(target, peer.Address));
    }
}
