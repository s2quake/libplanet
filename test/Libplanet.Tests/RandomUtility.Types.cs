using System.Security.Cryptography;
using Libplanet.Types;

namespace Libplanet.Tests;

public static partial class RandomUtility
{
    public static HashDigest<T> HashDigest<T>()
        where T : HashAlgorithm
    {
        return new HashDigest<T>(Array(Byte, Types.HashDigest<T>.Size));
    }

    public static Address Address()
    {
        var bytes = Array(Byte, Types.Address.Size);
        return new Address(bytes);
    }

    public static TxId TxId()
    {
        var bytes = Array(Byte, Types.TxId.Size);
        return new TxId(bytes);
    }

    public static BlockHash BlockHash()
    {
        var bytes = Array(Byte, Types.BlockHash.Size);
        return new BlockHash(bytes);
    }

    public static EvidenceId EvidenceId()
    {
        var bytes = Array(Byte, Types.EvidenceId.Size);
        return new EvidenceId(bytes);
    }
}
