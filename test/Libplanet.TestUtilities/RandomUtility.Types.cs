using System.Security.Cryptography;
using Libplanet.Data;
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

    public static ISigner Signer() => PrivateKey().AsSigner();

    public static ISigner Signer(Random random) => PrivateKey(random).AsSigner();

    public static Address Address() => Address(System.Random.Shared);

    public static Address Address(Random random) => new(Array(random, Byte, Types.Address.Size));

    public static TxId TxId() => TxId(System.Random.Shared);

    public static TxId TxId(Random random) => new(Array(random, Byte, Types.TxId.Size));

    public static BlockHash BlockHash() => BlockHash(System.Random.Shared);

    public static BlockHash BlockHash(Random random) => new(Array(random, Byte, Types.BlockHash.Size));

    public static EvidenceId EvidenceId() => EvidenceId(System.Random.Shared);

    public static EvidenceId EvidenceId(Random random) => new(Array(random, Byte, Types.EvidenceId.Size));

    public static BlockHeader BlockHeader(Random random, int? version = null, ISigner? proposer = null)
    {
        var height = Try(random, NonNegative, item => item > 1);
        var previousBlockHash = BlockHash(random);
        var previousBlockCommit = BlockCommit(random, height: height - 1, blockHash: previousBlockHash);
        proposer ??= Signer(random);
        return new()
        {
            Version = version ?? Types.BlockHeader.CurrentVersion,
            Height = height,
            Timestamp = DateTimeOffset(random),
            Proposer = proposer.Address,
            PreviousBlockHash = previousBlockHash,
            PreviousBlockCommit = previousBlockCommit,
            PreviousStateRootHash = HashDigest<SHA256>(random),
        };
    }

    public static BlockDigest BlockDigest(Random random) => new()
    {
        BlockHash = BlockHash(random),
        Header = BlockHeader(random),
        Signature = ImmutableArray(random, Byte),
        TxIds = ImmutableSortedSet(random, TxId),
        EvidenceIds = ImmutableSortedSet(random, EvidenceId),
    };

    public static BlockCommit BlockCommit() => BlockCommit(System.Random.Shared);

    public static BlockCommit BlockCommit(int height) => BlockCommit(System.Random.Shared);

    public static BlockCommit BlockCommit(
        Random random,
        BlockHash? blockHash = null,
        int? height = null,
        int? round = null,
        TestValidator[]? validators = null)
    {
        blockHash ??= BlockHash(random);
        height ??= Positive(random);
        round ??= NonNegative(random);
        validators ??= Array(random, TestValidator);
        var votes = validators.Select(validator => new VoteMetadata
        {
            Validator = validator.Address,
            BlockHash = blockHash.Value,
            Height = height.Value,
            Round = round.Value,
            Timestamp = DateTimeOffset(random),
            ValidatorPower = validator.Power,
            Type = Boolean(random) ? VoteType.PreCommit : VoteType.Null,
        }.Sign(validator));

        return new()
        {
            BlockHash = blockHash.Value,
            Height = height.Value,
            Round = round.Value,
            Votes = [.. votes],
        };
    }

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

    public static TransactionMetadata TransactionMetadata(Random random, Address? signer = null) => new()
    {
        Nonce = NonNegativeInt64(random),
        Signer = signer ?? Address(random),
        GenesisBlockHash = BlockHash(random),
        Actions = ImmutableArray(random, ActionBytecode),
        Timestamp = DateTimeOffset(random),
        MaxGasPrice = Nullable(random, FungibleAssetValue),
        GasLimit = NonNegativeInt64(random),
    };

    public static Transaction Transaction() => Transaction(System.Random.Shared);

    public static Transaction Transaction(Random random) => Transaction(random, signer: null);

    public static Transaction Transaction(Random random, ISigner? signer = null)
    {
        signer ??= Signer(random);
        var metadata = TransactionMetadata(random, signer.Address);
        return metadata.Sign(signer);
    }

    public static EvidenceBase Evidence() => Evidence(System.Random.Shared);

    public static EvidenceBase Evidence(Random random) => new TestEvidence
    {
        Height = NonNegative(random),
        TargetAddress = Address(random),
        Timestamp = DateTimeOffset(random),
    };

    public static TransactionExecutionInfo Txexecution() => TxExecution(System.Random.Shared);

    public static TransactionExecutionInfo TxExecution(Random random) => new()
    {
        TxId = TxId(random),
        BlockHash = BlockHash(random),
        EnterState = HashDigest<SHA256>(random),
        LeaveState = HashDigest<SHA256>(random),
        ExceptionNames = ImmutableArray(random, String),
    };

    public static BlockExecutionInfo BlockExecution() => BlockExecution(System.Random.Shared);

    public static BlockExecutionInfo BlockExecution(Random random) => new()
    {
        BlockHash = BlockHash(random),
        EnterState = HashDigest<SHA256>(random),
        LeaveState = HashDigest<SHA256>(random),
    };

    public static BlockContent BlockContent() => BlockContent(System.Random.Shared);

    public static BlockContent BlockContent(Random random) => new()
    {
        Transactions = ImmutableSortedSet(random, Transaction),
        Evidence = ImmutableSortedSet(random, Evidence),
    };

    public static Block Block() => Block(System.Random.Shared);

    public static Block Block(Random random)
    {
        var proposer = Signer(random);
        var rawBlock = new RawBlock
        {
            Header = BlockHeader(random, proposer: proposer),
            Content = BlockContent(random),
        }.Sign(proposer);
        return rawBlock;
    }

    public static Validator Validator() => Validator(System.Random.Shared);

    public static Validator Validator(Random random) => new()
    {
        Address = Address(random),
        Power = NonNegativeBigInteger(random),
    };

    public static TestValidator TestValidator() => TestValidator(System.Random.Shared);

    public static TestValidator TestValidator(Random random) => new(Signer(random), NonNegativeBigInteger(random));
}
