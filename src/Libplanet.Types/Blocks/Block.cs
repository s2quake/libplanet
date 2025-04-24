using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Libplanet.Common;
using Libplanet.Common.JsonConverters;
using Libplanet.Crypto;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Types.Blocks;

public sealed record class Block(BlockHeader Header, PreEvaluationBlock PreEvaluationBlock)
{
    public const int CurrentProtocolVersion = BlockMetadata.CurrentProtocolVersion;

    // private readonly BlockHeader _header;
    // private readonly PreEvaluationBlock _preEvaluationBlock;

    // public Block(
    //     IBlockHeader header,
    //     IEnumerable<Transaction> transactions,
    //     IEnumerable<EvidenceBase> evidence)
    //     : this(
    //         new PreEvaluationBlock(header, transactions, evidence),
    //         (header.StateRootHash, header.Signature, header.Hash))
    // {
    // }

    // public Block(
    //     PreEvaluationBlock preEvaluationBlock,
    //     (
    //         HashDigest<SHA256> StateRootHash,
    //         ImmutableArray<byte>? Signature,
    //         BlockHash Hash
    //     ) proof
    // )
    // {
    //     _header = new BlockHeader(preEvaluationBlock.Header, proof);
    //     _preEvaluationBlock = preEvaluationBlock;
    // }

    // [JsonIgnore]
    // public BlockHeader Header => _header;

    public int ProtocolVersion => PreEvaluationBlock.ProtocolVersion;

    public BlockHash Hash => Header.BlockHash;

    public ImmutableArray<byte> Signature => Header.Signature;

    public HashDigest<SHA256> PreEvaluationHash => PreEvaluationBlock.PreEvaluationHash;

    public HashDigest<SHA256> StateRootHash => Header.StateRootHash;

    public long Index => PreEvaluationBlock.Index;

    public Address Miner => PreEvaluationBlock.Miner;

    public PublicKey? PublicKey => PreEvaluationBlock.PublicKey;

    public BlockHash PreviousHash => PreEvaluationBlock.PreviousHash;

    public DateTimeOffset Timestamp => PreEvaluationBlock.Timestamp;

    public HashDigest<SHA256>? TxHash => PreEvaluationBlock.TxHash;

    public BlockCommit? LastCommit => PreEvaluationBlock.LastCommit;

    public HashDigest<SHA256>? EvidenceHash => PreEvaluationBlock.EvidenceHash;

    public ImmutableSortedSet<EvidenceBase> Evidence => PreEvaluationBlock.Evidence;

    public ImmutableSortedSet<Transaction> Transactions => PreEvaluationBlock.Transactions;

    public override int GetHashCode() => unchecked((17 * 31 + Hash.GetHashCode()) * 31);

    public override string ToString() => Hash.ToString();
}
