using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Types.Blocks;

public sealed record class RawBlock(BlockContent Content, RawBlockHeader Header)
{
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

    public HashDigest<SHA256> RawHash => Header.RawHash;

    public Block Sign(PrivateKey privateKey, HashDigest<SHA256> stateRootHash)
    {
        var signature = Header.MakeSignature(privateKey, stateRootHash);
        var blockHash = Header.DeriveBlockHash(stateRootHash, signature);
        var header = new BlockHeader(Header, stateRootHash, signature, blockHash);
        return new Block(header, this);
    }
}
