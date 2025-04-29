using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Types.Blocks;

[Model(Version = 1)]
public sealed record class RawBlock
{
    [Property(0)]
    public required BlockMetadata Metadata { get; init; }

    [Property(1)]
    public required HashDigest<SHA256> RawHash { get; init; }

    [Property(2)]
    public required BlockContent Content { get; init; }

    public ImmutableSortedSet<Transaction> Transactions => Content.Transactions;

    public ImmutableSortedSet<EvidenceBase> Evidence => Content.Evidence;

    public int ProtocolVersion => Metadata.ProtocolVersion;

    public long Height => Metadata.Height;

    public DateTimeOffset Timestamp => Metadata.Timestamp;

    public Address Miner => Metadata.Miner;

    public PublicKey? PublicKey => Metadata.PublicKey;

    public BlockHash PreviousHash => Metadata.PreviousHash;

    public HashDigest<SHA256>? TxHash => Metadata.TxHash;

    public BlockCommit LastCommit => Metadata.LastCommit;

    public HashDigest<SHA256>? EvidenceHash => Metadata.EvidenceHash;

    public static explicit operator RawBlock(Block block)
    {
        return new RawBlock
        {
            Metadata = (BlockMetadata)block.Header,
            RawHash = block.RawHash,
            Content = block.Content,
        };
    }

    public static RawBlock Propose(BlockMetadata metadata)
        => Propose(metadata, new BlockContent());

    public static RawBlock Propose(BlockMetadata metadata, BlockContent content)
    {
        var preEvaluationHash = metadata.DerivePreEvaluationHash();
        // var header = new RawBlockHeader
        // {
        //     Metadata = Metadata, 
        //     RawHash = preEvaluationHash,
        // };
        return new RawBlock
        {
            Metadata = metadata,
            RawHash = preEvaluationHash,
            Content = content,
        };
    }

    public Block Sign(PrivateKey privateKey, HashDigest<SHA256> stateRootHash)
    {
        var signature = Metadata.MakeSignature(privateKey, stateRootHash);
        var blockHash = Metadata.DeriveBlockHash(stateRootHash, signature);
        var header = new BlockHeader
        {
            StateRootHash = stateRootHash,
            Signature = signature,
            BlockHash = blockHash,
            ProtocolVersion = Metadata.ProtocolVersion,
            Height = Metadata.Height,
            Timestamp = Metadata.Timestamp,
            Miner = Metadata.Miner,
            PublicKey = Metadata.PublicKey,
            PreviousHash = Metadata.PreviousHash,
            TxHash = Metadata.TxHash,
            LastCommit = Metadata.LastCommit,
            EvidenceHash = Metadata.EvidenceHash,
            RawHash = Metadata.DerivePreEvaluationHash(),
        };
        return new Block { Header = header, Content = Content };
    }

    public void ValidateTimestamp() => ValidateTimestamp(DateTimeOffset.UtcNow);

    public void ValidateTimestamp(DateTimeOffset currentTime)
        => Metadata.ValidateTimestamp(currentTime);
}
