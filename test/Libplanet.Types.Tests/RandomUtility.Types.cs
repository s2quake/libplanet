using System.Security.Cryptography;
using Libplanet.Data;
using Libplanet.Types;

namespace Libplanet.Types.Tests;

public static partial class RandomUtility
{
    public static HashDigest<T> HashDigest<T>()
        where T : HashAlgorithm
        => HashDigest<T>(System.Random.Shared);

    public static HashDigest<T> HashDigest<T>(Random random)
        where T : HashAlgorithm
        => new(Array(random, Byte, Types.HashDigest<T>.Size));

    public static Address Address() => Address(System.Random.Shared);

    public static Address Address(Random random)
    {
        var bytes = Array(random, Byte, Types.Address.Size);
        return new Address(bytes);
    }

    public static TxId TxId() => TxId(System.Random.Shared);

    public static TxId TxId(Random random) => new(Array(random, Byte, Types.TxId.Size));

    public static BlockHash BlockHash()
    {
        var bytes = Array(Byte, Types.BlockHash.Size);
        return new BlockHash(bytes);
    }

    public static BlockHash BlockHash(Random random)
    {
        var bytes = Array(random, Byte, Types.BlockHash.Size);
        return new BlockHash(bytes);
    }

    public static EvidenceId EvidenceId() => EvidenceId(System.Random.Shared);

    public static EvidenceId EvidenceId(Random random) => new(Array(random, Byte, Types.EvidenceId.Size));

    public static BlockHeader BlockHeader(Random random) => new()
    {
        Version = Int32(random),
        Height = Int32(random),
        Timestamp = DateTimeOffset(random),
        Proposer = Address(random),
        PreviousHash = BlockHash(random),
        PreviousCommit = BlockCommit(random),
        PreviousStateRootHash = HashDigest<SHA256>(random),
    };

    public static BlockDigest BlockDigest(Random random) => new()
    {
        BlockHash = BlockHash(random),
        Header = BlockHeader(random),
        Signature = ImmutableArray(random, Byte),
        TxIds = ImmutableSortedSet(random, TxId),
        EvidenceIds = ImmutableSortedSet(random, EvidenceId),
    };

    public static BlockCommit BlockCommit(Random random) => new()
    {
        BlockHash = BlockHash(random),
        Height = Int32(random),
        Round = Int32(random),
        Votes = ImmutableArray(random, Vote),
    };

    public static Vote Vote(Random random) => new()
    {
        Metadata = VoteMetadata(random),
        Signature = ImmutableArray(random, Byte),
    };

    public static VoteMetadata VoteMetadata(Random random) => new()
    {
        Validator = Address(random),
        BlockHash = BlockHash(random),
        Height = Int32(random),
        Round = Int32(random),
        Timestamp = DateTimeOffset(random),
        ValidatorPower = BigInteger(random),
        Flag = Enum<VoteFlag>(random),
    };
}
