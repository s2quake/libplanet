using System.Security.Cryptography;
using Destructurama.Attributed;

namespace Libplanet.Net.Messages;

public abstract class MessageContent
{
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

    public abstract MessageType Type { get; }

    public abstract IEnumerable<byte[]> DataFrames { get; }

    [NotLogged]
    public MessageId Id
    {
        get
        {
            var bytes = new List<byte>();
            bytes.AddRange(BitConverter.GetBytes((int)Type));
            foreach (byte[] ba in DataFrames)
            {
                bytes.AddRange(ba);
            }

            var digest = SHA256.HashData([.. bytes]);
            return new MessageId(digest);
        }
    }
}
