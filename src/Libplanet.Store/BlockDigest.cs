using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Types.Blocks;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Store;

[Model(Version = 1)]
public readonly record struct BlockDigest
{
    private static readonly Codec _codec = new();

    private readonly BlockMetadata _metadata;
    private readonly HashDigest<SHA256> _preEvaluationHash;

    public BlockDigest(
        BlockHeader header,
        ImmutableArray<ImmutableArray<byte>> txIds,
        ImmutableArray<ImmutableArray<byte>> evidenceIds)
    {
        // _metadata = header.RawBlockHeader.Metadata;
        _preEvaluationHash = header.RawHash;
        StateRootHash = header.StateRootHash;
        Signature = header.Signature;
        Hash = header.BlockHash;
        TxIds = txIds;
        EvidenceIds = evidenceIds;
    }

    public int ProtocolVersion => _metadata.ProtocolVersion;

    public long Index => _metadata.Index;

    public DateTimeOffset Timestamp => _metadata.Timestamp;

    public Address Miner => _metadata.Miner;

    public PublicKey? PublicKey => _metadata.PublicKey;

    public BlockHash PreviousHash => _metadata.PreviousHash;

    public HashDigest<SHA256>? TxHash => _metadata.TxHash;

    public BlockCommit? LastCommit => _metadata.LastCommit;

    public HashDigest<SHA256>? EvidenceHash => _metadata.EvidenceHash;

    public BlockHash Hash { get; }

    public HashDigest<SHA256> StateRootHash { get; }

    public ImmutableArray<byte>? Signature { get; }

    public ImmutableArray<ImmutableArray<byte>> TxIds { get; }

    public ImmutableArray<ImmutableArray<byte>> EvidenceIds { get; }

    public static BlockDigest FromBlock(Block block)
    {
        return new BlockDigest(
            header: block.Header,
            txIds: [.. block.Transactions.Select(tx => tx.Id.Bytes)],
            evidenceIds: [.. block.Evidence.Select(ev => ev.Id.Bytes)]
        );
    }

    public BlockHeader GetHeader()
    {
        // var preEvalHeader = new RawBlockHeader
        // {
        //     Metadata = _metadata,
        //     RawHash = _preEvaluationHash,
        // };
        // return new BlockHeader(preEvalHeader, StateRootHash, Signature ?? [], Hash);

        throw new NotImplementedException();
    }
}
