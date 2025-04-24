using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Types.Blocks;

public sealed record class PreEvaluationBlock(
    BlockContent Content,
    PreEvaluationBlockHeader Header)
{
    // public PreEvaluationBlock(
    //     PreEvaluationBlockHeader preEvaluationBlockHeader,
    //     IEnumerable<Transaction> transactions,
    //     IEnumerable<EvidenceBase> evidence)
    //     : this(
    //         new BlockContent(preEvaluationBlockHeader, transactions, evidence),
    //         preEvaluationBlockHeader.PreEvaluationHash)
    // {
    // }

    public ImmutableSortedSet<Transaction> Transactions => Content.Transactions;

    public ImmutableSortedSet<EvidenceBase> Evidence => Content.Evidence;

    public int ProtocolVersion => Header.ProtocolVersion;

    public long Index => Header.Index;

    public DateTimeOffset Timestamp => Header.Timestamp;

    public Address Miner => Header.Miner;

    public PublicKey? PublicKey => Header.PublicKey;

    public BlockHash PreviousHash => Header.PreviousHash;

    public HashDigest<SHA256>? TxHash => Header.TxHash;

    public BlockCommit? LastCommit => Header.LastCommit;

    public HashDigest<SHA256>? EvidenceHash => Header.EvidenceHash;

    public HashDigest<SHA256> PreEvaluationHash => Header.PreEvaluationHash;

    public Block Sign(PrivateKey privateKey, HashDigest<SHA256> stateRootHash)
    {
        var signature = Header.MakeSignature(privateKey, stateRootHash);
        var header = new BlockHeader
        return new Block(
            this, (stateRootHash, signature, Header.DeriveBlockHash(stateRootHash, signature)));
    }
}
