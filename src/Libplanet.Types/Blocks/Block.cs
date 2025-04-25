using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Types.Blocks;

[Model(Version = 1)]
public sealed record class Block
{
    public const int CurrentProtocolVersion = BlockMetadata.CurrentProtocolVersion;

    // private readonly BlockHeader _header;
    // private readonly RawBlock _preEvaluationBlock;

    public static Block Create(
        BlockHeader header,
        ImmutableSortedSet<Transaction> transactions,
        ImmutableSortedSet<EvidenceBase> evidence)
    {
        // var rawHeader = header.RawBlockHeader;
        // var metadata = rawHeader.Metadata;
        // var blockContent = new BlockContent
        // {
        //     Metadata = metadata,
        //     Transactions = transactions,
        //     Evidence = evidence,
        // };
        // var rawBlock = new RawBlock { Content = blockContent, Header = rawHeader };
        // return new Block(header, rawBlock);
        return new Block
        {
            Header = header,
            Content = new BlockContent
            {
                Transactions = transactions,
                Evidence = evidence,
            },
        };
    }

    public static Block Create(
        RawBlock rawBlock,
        (
            HashDigest<SHA256> StateRootHash,
            ImmutableArray<byte>? Signature,
            BlockHash Hash
        ) proof
    )
    {
        var content = rawBlock.Content;
        var header = new BlockHeader
        {

        };
        // return new Block(
        //     new BlockHeader(
        //         rawBlock.Header, proof.StateRootHash, proof.Signature ?? [], proof.Hash),
        //     rawBlock);
        throw new NotImplementedException();
    }

    // [JsonIgnore]
    // public BlockHeader Header => _header;

    public required BlockHeader Header { get; init; }

    public required BlockContent Content { get; init; }

    public int ProtocolVersion => Header.ProtocolVersion;

    public BlockHash Hash => Header.BlockHash;

    public ImmutableArray<byte> Signature => Header.Signature;

    public HashDigest<SHA256> RawHash => Header.RawHash;

    public HashDigest<SHA256> StateRootHash => Header.StateRootHash;

    public long Index => Header.Index;

    public Address Miner => Header.Miner;

    public PublicKey? PublicKey => Header.PublicKey;

    public BlockHash PreviousHash => Header.PreviousHash;

    public DateTimeOffset Timestamp => Header.Timestamp;

    public HashDigest<SHA256>? TxHash => Header.TxHash;

    public BlockCommit? LastCommit => Header.LastCommit;

    public HashDigest<SHA256>? EvidenceHash => Header.EvidenceHash;

    public ImmutableSortedSet<EvidenceBase> Evidence => Content.Evidence;

    public ImmutableSortedSet<Transaction> Transactions => Content.Transactions;

    public override int GetHashCode() => unchecked((17 * 31 + Hash.GetHashCode()) * 31);

    public override string ToString() => Hash.ToString();

    // public void ValidateTimestamp() => ValidateTimestamp(DateTimeOffset.UtcNow);

    // public void ValidateTimestamp(DateTimeOffset currentTime)
    // => Header.RawBlockHeader.Metadata.ValidateTimestamp(currentTime);

    public string ToExcerptString()
    {
        return
            $"{GetType().Name} {{" +
            $" {nameof(ProtocolVersion)} = {ProtocolVersion}," +
            $" {nameof(Index)} = {Index}," +
            $" {nameof(Hash)} = {Hash}," +
            " }";
    }
}
