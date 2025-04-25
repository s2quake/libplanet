using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Types.Blocks;

[Model(Version = 1)]
public sealed record class Block(
    [property: Property(0)] BlockHeader Header,
    [property: Property(1)] RawBlock RawBlock)
{
    public const int CurrentProtocolVersion = BlockMetadata.CurrentProtocolVersion;
    private static readonly TimeSpan TimestampThreshold = TimeSpan.FromSeconds(15);

    // private readonly BlockHeader _header;
    // private readonly RawBlock _preEvaluationBlock;

    public static Block Create(
        BlockHeader header,
        ImmutableSortedSet<Transaction> transactions,
        ImmutableSortedSet<EvidenceBase> evidence)
    {
        var rawHeader = header.RawBlockHeader;
        var metadata = rawHeader.Metadata;
        var blockContent = new BlockContent
        {
            Metadata = metadata,
            Transactions = transactions,
            Evidence = evidence,
        };
        var rawBlock = new RawBlock { Content = blockContent, Header = rawHeader };
        return new Block(header, rawBlock);
    }

    // public Block(
    //     RawBlock rawBlock,
    //     (
    //         HashDigest<SHA256> StateRootHash,
    //         ImmutableArray<byte>? Signature,
    //         BlockHash Hash
    //     ) proof
    // )
    // {
    //     _header = new BlockHeader(rawBlock.Header, proof);
    //     _preEvaluationBlock = rawBlock;
    // }

    // [JsonIgnore]
    // public BlockHeader Header => _header;

    public int ProtocolVersion => RawBlock.ProtocolVersion;

    public BlockHash Hash => Header.BlockHash;

    public ImmutableArray<byte> Signature => Header.Signature;

    public HashDigest<SHA256> RawHash => RawBlock.RawHash;

    public HashDigest<SHA256> StateRootHash => Header.StateRootHash;

    public long Index => RawBlock.Index;

    public Address Miner => RawBlock.Miner;

    public PublicKey? PublicKey => RawBlock.PublicKey;

    public BlockHash PreviousHash => RawBlock.PreviousHash;

    public DateTimeOffset Timestamp => RawBlock.Timestamp;

    public HashDigest<SHA256>? TxHash => RawBlock.TxHash;

    public BlockCommit? LastCommit => RawBlock.LastCommit;

    public HashDigest<SHA256>? EvidenceHash => RawBlock.EvidenceHash;

    public ImmutableSortedSet<EvidenceBase> Evidence => RawBlock.Evidence;

    public ImmutableSortedSet<Transaction> Transactions => RawBlock.Transactions;

    public override int GetHashCode() => unchecked((17 * 31 + Hash.GetHashCode()) * 31);

    public override string ToString() => Hash.ToString();

    public void ValidateTimestamp() => ValidateTimestamp(DateTimeOffset.UtcNow);

    public void ValidateTimestamp(DateTimeOffset currentTime)
    {
        if (currentTime + TimestampThreshold < Header.RawBlockHeader.Metadata.Timestamp)
        {
            var message = $"The block #{Header.Index}'s timestamp " +
                $"({Header.RawBlockHeader.Metadata.Timestamp}) is later than now " +
                $"({currentTime}, threshold: {TimestampThreshold}).";
            throw new InvalidOperationException(message);
            // string hash = metadata is IBlockExcerpt h
            //     ? $" {h.Hash}"
            //     : string.Empty;
            // throw new InvalidOperationException(
            //     $"The block #{metadata.Index}{hash}'s timestamp " +
            //     $"({metadata.Timestamp}) is later than now ({currentTime}, " +
            //     $"threshold: {TimestampThreshold}).");
        }
    }
}
