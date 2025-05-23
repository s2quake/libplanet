using System.Security.Cryptography;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;
using Libplanet.Types.Evidence;
using Libplanet.Types.Transactions;

namespace Libplanet.Tests;

public static partial class RandomUtility
{
    public static HashDigest<T> NextHashDigest<T>()
        where T : HashAlgorithm
    {
        return new HashDigest<T>(Array(Byte, HashDigest<T>.Size));
    }

    public static Address Address()
    {
        var bytes = Array(Byte, Types.Crypto.Address.Size);
        return new Address(bytes);
    }

    public static TxId TxId()
    {
        var bytes = Array(Byte, Types.Tx.TxId.Size);
        return new TxId(bytes);
    }

    public static BlockHash BlockHash()
    {
        var bytes = Array(Byte, Types.Blocks.BlockHash.Size);
        return new BlockHash(bytes);
    }

    public static EvidenceId EvidenceId()
    {
        var bytes = Array(Byte, Types.Evidence.EvidenceId.Size);
        return new EvidenceId(bytes);
    }
}
