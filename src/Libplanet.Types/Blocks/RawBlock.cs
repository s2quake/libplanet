using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Types.Crypto;

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

    public static explicit operator RawBlock(Block block) => new()
    {
        Metadata = (BlockMetadata)block.Header,
        RawHash = block.RawHash,
        Content = block.Content,
    };

    public static RawBlock Propose(BlockMetadata metadata) => Propose(metadata, new BlockContent());

    public static RawBlock Propose(BlockMetadata metadata, BlockContent content)
    {
        var rawHash = metadata.DerivePreEvaluationHash();
        return new RawBlock
        {
            Metadata = metadata,
            RawHash = rawHash,
            Content = content,
        };
    }

    public Block Sign(PrivateKey privateKey, HashDigest<SHA256> stateRootHash)
    {
        var signature = BlockMetadata.MakeSignature(privateKey, stateRootHash);
        var blockHash = Metadata.DeriveBlockHash(stateRootHash, signature);
        var header = new BlockHeader
        {
            StateRootHash = stateRootHash,
            Signature = signature,
            BlockHash = blockHash,
            ProtocolVersion = Metadata.ProtocolVersion,
            Height = Metadata.Height,
            Timestamp = Metadata.Timestamp,
            Proposer = Metadata.Proposer,
            PreviousHash = Metadata.PreviousHash,
            LastCommit = Metadata.LastCommit,
            RawHash = Metadata.DerivePreEvaluationHash(),
            TxHash = Content.TxHash,
            EvidenceHash = Content.EvidenceHash,
        };
        return new Block { Header = header, Content = Content };
    }

    public void ValidateTimestamp() => ValidateTimestamp(DateTimeOffset.UtcNow);

    public void ValidateTimestamp(DateTimeOffset currentTime) => Metadata.ValidateTimestamp(currentTime);
}
