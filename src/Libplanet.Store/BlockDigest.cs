using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Types.Blocks;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Store;

[Model(Version = 1)]
public sealed record class BlockDigest
{
    // private static readonly Codec _codec = new();

    // private readonly BlockMetadata _metadata;
    // private readonly HashDigest<SHA256> _preEvaluationHash;

    // public BlockDigest(
    //     BlockHeader header,
    //     ImmutableArray<ImmutableArray<byte>> txIds,
    //     ImmutableArray<ImmutableArray<byte>> evidenceIds)
    // {
    //     // _metadata = header.RawBlockHeader.Metadata;
    //     // _preEvaluationHash = header.RawHash;
    //     StateRootHash = header.StateRootHash;
    //     Signature = header.Signature;
    //     Hash = header.BlockHash;
    //     TxIds = txIds;
    //     EvidenceIds = evidenceIds;
    // }

    [Property(0)]
    public required BlockHeader Header { get; init; }

    // public int ProtocolVersion => _metadata.ProtocolVersion;

    public long Height => Header.Height;

    // public DateTimeOffset Timestamp => _metadata.Timestamp;

    public Address Miner => Header.Miner;

    // public PublicKey? PublicKey => _metadata.PublicKey;

    public BlockHash PreviousHash => Header.PreviousHash;

    // public HashDigest<SHA256>? TxHash => _metadata.TxHash;

    // public BlockCommit LastCommit => _metadata.LastCommit;

    // public HashDigest<SHA256> EvidenceHash => _metadata.EvidenceHash;

    public BlockHash Hash => Header.BlockHash;

    public HashDigest<SHA256> StateRootHash => Header.StateRootHash;

    // public ImmutableArray<byte> Signature { get; }

    [Property(1)]
    public ImmutableSortedSet<TxId> TxIds { get; init; } = [];

    [Property(2)]
    public ImmutableSortedSet<EvidenceId> EvidenceIds { get; init; } = [];

    public static BlockDigest Create(Block block)
    {
        return new BlockDigest
        {
            Header = block.Header,
            TxIds = [.. block.Transactions.Select(tx => tx.Id)],
            EvidenceIds = [.. block.Evidence.Select(ev => ev.Id)],
        };
        // return new BlockDigest(
        //     header: block.Header,
        //     txIds: [.. block.Transactions.Select(tx => tx.Id.Bytes)],
        //     evidenceIds: [.. block.Evidence.Select(ev => ev.Id.Bytes)]
        // );
    }

    // public BlockHeader GetHeader() =>
    // {
    //     // var preEvalHeader = new RawBlockHeader
    //     // {
    //     //     Metadata = _metadata,
    //     //     RawHash = _preEvaluationHash,
    //     // };
    //     // return new BlockHeader(preEvalHeader, StateRootHash, Signature ?? [], Hash);

    //     throw new NotImplementedException();
    // }
}
