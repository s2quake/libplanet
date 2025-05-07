using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Types.Crypto;

namespace Libplanet.Types.Blocks;

[Model(Version = 1)]
public sealed record class RawBlock
{
    [Property(0)]
    public required BlockHeader Header { get; init; }

    [Property(1)]
    public required HashDigest<SHA256> RawHash { get; init; }

    [Property(2)]
    public required BlockContent Content { get; init; }

    public static explicit operator RawBlock(Block block) => new()
    {
        Header = block.Header,
        RawHash = block.RawHash,
        Content = block.Content,
    };

    public static RawBlock Propose(BlockHeader header) => Propose(header, new BlockContent());

    public static RawBlock Propose(BlockHeader header, BlockContent content)
    {
        var rawHash = header.DerivePreEvaluationHash();
        return new RawBlock
        {
            Header = header,
            RawHash = rawHash,
            Content = content,
        };
    }

    public Block Sign(PrivateKey privateKey, HashDigest<SHA256> stateRootHash)
    {
        var signature = BlockHeader.MakeSignature(privateKey, stateRootHash);
        var blockHash = Header.DeriveBlockHash(stateRootHash, signature);
        // var header = new BlockHeader
        // {
        //     StateRootHash = stateRootHash,
        //     Signature = signature,
        //     BlockHash = blockHash,
        //     ProtocolVersion = Metadata.ProtocolVersion,
        //     Height = Metadata.Height,
        //     Timestamp = Metadata.Timestamp,
        //     Proposer = Metadata.Proposer,
        //     PreviousHash = Metadata.PreviousHash,
        //     LastCommit = Metadata.LastCommit,
        //     RawHash = Metadata.DerivePreEvaluationHash(),
        //     TxHash = Content.TxHash,
        //     EvidenceHash = Content.EvidenceHash,
        // };
        return new Block { Header = Header, Content = Content };
    }

    public void ValidateTimestamp() => ValidateTimestamp(DateTimeOffset.UtcNow);

    public void ValidateTimestamp(DateTimeOffset currentTime) => Header.ValidateTimestamp(currentTime);
}
