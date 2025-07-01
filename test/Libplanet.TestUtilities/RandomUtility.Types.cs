using System.Security.Cryptography;
using Libplanet.Data;
using Libplanet.Net;
using Libplanet.Types;

namespace Libplanet.TestUtilities;

public static partial class RandomUtility
{
    public static HashDigest<T> HashDigest<T>()
        where T : HashAlgorithm
        => HashDigest<T>(System.Random.Shared);

    public static HashDigest<T> HashDigest<T>(Random random)
        where T : HashAlgorithm
        => new(Array(random, Byte, Types.HashDigest<T>.Size));

    public static PrivateKey PrivateKey() => PrivateKey(System.Random.Shared);

    public static PrivateKey PrivateKey(Random random) => new(Array(random, Byte, Types.PrivateKey.Size));

    public static Address Address() => Address(System.Random.Shared);

    public static Address Address(Random random) => new(Array(random, Byte, Types.Address.Size));

    public static TxId TxId() => TxId(System.Random.Shared);

    public static TxId TxId(Random random) => new(Array(random, Byte, Types.TxId.Size));

    public static BlockHash BlockHash() => BlockHash(System.Random.Shared);

    public static BlockHash BlockHash(Random random) => new BlockHash(Array(random, Byte, Types.BlockHash.Size));

    public static EvidenceId EvidenceId() => EvidenceId(System.Random.Shared);

    public static EvidenceId EvidenceId(Random random) => new(Array(random, Byte, Types.EvidenceId.Size));

    public static BlockHeader BlockHeader(Random random) => new()
    {
        Version = NonNegative(random),
        Height = NonNegative(random),
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

    public static BlockCommit BlockCommit() => BlockCommit(System.Random.Shared);

    public static BlockCommit BlockCommit(Random random) => new()
    {
        BlockHash = BlockHash(random),
        Height = Int32(random),
        Round = Int32(random),
        Votes = ImmutableArray(random, Vote),
    };

    public static Vote Vote(Random random)
    {
        var key = PrivateKey(random);
        var metadata = VoteMetadata(random) with
        {
            Validator = key.Address,
        };
        return metadata.Sign(key.AsSigner());
    }

    public static VoteMetadata VoteMetadata(Random random) => new()
    {
        Validator = Address(random),
        BlockHash = BlockHash(random),
        Height = NonNegative(random),
        Round = NonNegative(random),
        Timestamp = DateTimeOffset(random),
        ValidatorPower = PositiveBigInteger(random),
        Type = Try(random, Enum<VoteType>, item => item is not VoteType.Null and not VoteType.Unknown),
    };

    public static string Ticker() => Ticker(System.Random.Shared);

    public static string Ticker(Random random)
    {
        var length = Int32(random, 3, 6);
        var items = Array(() => (char)Int32(random, 'A', 'Z' + 1), length);
        return new string(items);
    }

    public static Currency Currency() => Currency(System.Random.Shared);

    public static Currency Currency(Random random) => new()
    {
        Ticker = Ticker(random),
        DecimalPlaces = Byte(random),
        MaximumSupply = Try(random, BigInteger, item => item > 0),
        Minters = ImmutableSortedSet(random, Address),
    };

    public static FungibleAssetValue FungibleAssetValue() => FungibleAssetValue(System.Random.Shared);

    public static FungibleAssetValue FungibleAssetValue(Random random) => new()
    {
        Currency = Currency(random),
        RawValue = Try(random, BigInteger, item => item >= 0),
    };

    public static ActionBytecode ActionBytecode() => ActionBytecode(System.Random.Shared);

    public static ActionBytecode ActionBytecode(Random random) => new(Array(random, Byte));

    public static TransactionMetadata TransactionMetadata() => TransactionMetadata(System.Random.Shared);

    public static TransactionMetadata TransactionMetadata(Random random) => new()
    {
        Nonce = Int64(random),
        Signer = Address(random),
        GenesisHash = BlockHash(random),
        Actions = ImmutableArray(random, ActionBytecode),
        Timestamp = DateTimeOffset(random),
        MaxGasPrice = Nullable(random, FungibleAssetValue),
        GasLimit = Try(random, Int64, item => item >= 0),
    };

    public static Transaction Transaction() => Transaction(System.Random.Shared);

    public static Transaction Transaction(Random random) => new()
    {
        Metadata = TransactionMetadata(random),
        Signature = ImmutableArray(random, Byte),
    };

    public static EvidenceBase Evidence() => Evidence(System.Random.Shared);

    public static EvidenceBase Evidence(Random random) => new TestEvidence
    {
        Height = NonNegative(random),
        TargetAddress = Address(random),
        Timestamp = DateTimeOffset(random),
    };

    public static TxExecution Txexecution() => TxExecution(System.Random.Shared);

    public static TxExecution TxExecution(Random random) => new()
    {
        TxId = TxId(random),
        BlockHash = BlockHash(random),
        InputState = HashDigest<SHA256>(random),
        OutputState = HashDigest<SHA256>(random),
        ExceptionNames = ImmutableArray(random, String),
    };

    public static BlockExecution BlockExecution() => BlockExecution(System.Random.Shared);

    public static BlockExecution BlockExecution(Random random) => new()
    {
        BlockHash = BlockHash(random),
        InputState = HashDigest<SHA256>(random),
        OutputState = HashDigest<SHA256>(random),
    };

    public static BlockContent BlockContent() => BlockContent(System.Random.Shared);

    public static BlockContent BlockContent(Random random) => new()
    {
        Transactions = ImmutableSortedSet(random, Transaction),
        Evidences = ImmutableSortedSet(random, Evidence),
    };

    public static Block Block() => Block(System.Random.Shared);

    public static Block Block(Random random) => new()
    {
        Header = BlockHeader(random),
        Content = BlockContent(random),
        Signature = ImmutableArray(random, Byte),
    };

    public static Peer Peer() => Peer(System.Random.Shared);

    public static Peer Peer(Random random) => new()
    {
        Address = Address(random),
        EndPoint = DnsEndPoint(random),
    };
}
