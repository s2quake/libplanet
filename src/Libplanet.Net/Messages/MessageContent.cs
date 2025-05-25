using System.Security.Cryptography;
using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

public abstract record class MessageContent
{
    private MessageId? _id;

    public enum MessageType : byte
    {
        Ping = 0x01,

        Pong = 0x14,

        GetBlockHashes = 0x032,

        TxIds = 0x31,

        GetBlocks = 0x07,

        GetTxs = 0x08,

        Blocks = 0x0a,

        Tx = 0x10,

        FindNeighbors = 0x11,

        Neighbors = 0x12,

        BlockHeaderMessage = 0x0c,

        BlockHashes = 0x33,

        GetChainStatus = 0x20,

        ChainStatus = 0x25,

        DifferentVersion = 0x30,

        HaveMessage = 0x43,

        WantMessage = 0x44,

        ConsensusProposal = 0x50,

        ConsensusVote = 0x51,

        ConsensusCommit = 0x52,

        ConsensusMaj23Msg = 0x53,

        ConsensusVoteSetBitsMsg = 0x54,

        ConsensusProposalClaimMsg = 0x55,

        EvidenceIds = 0x56,

        GetEvidence = 0x57,

        Evidence = 0x58,
    }

    [Property(0, ReadOnly = true)]
    public abstract MessageType Type { get; }

    public MessageId Id => _id ??= new MessageId(SHA256.HashData(ModelSerializer.SerializeToBytes(this)));
}
