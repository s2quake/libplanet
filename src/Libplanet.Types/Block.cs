using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;

namespace Libplanet.Types;

[Model(Version = 1, TypeName = "blk")]
public sealed partial record class Block
{
    [Property(0)]
    public required BlockHeader Header { get; init; }

    [Property(1)]
    public required BlockContent Content { get; init; }

    [Property(2)]
    [NotDefault]
    public required ImmutableArray<byte> Signature { get; init; }

    public BlockHash BlockHash => BlockHash.HashData(ModelSerializer.SerializeToBytes(this));

    public int Height => Header.Height;

    public int Version => Header.Version;

    public DateTimeOffset Timestamp => Header.Timestamp;

    public Address Proposer => Header.Proposer;

    public BlockHash PreviousBlockHash => Header.PreviousBlockHash;

    public BlockCommit PreviousBlockCommit => Header.PreviousBlockCommit;

    public HashDigest<SHA256> PreviousStateRootHash => Header.PreviousStateRootHash;

    public ImmutableSortedSet<Transaction> Transactions => Content.Transactions;

    public ImmutableSortedSet<EvidenceBase> Evidences => Content.Evidence;

    public override string ToString() => BlockHash.ToString();

    public bool Verify()
    {
        var rawBlock = new RawBlock
        {
            Header = Header,
            Content = Content,
        };
        var bytes = ModelSerializer.SerializeToBytes(rawBlock);
        return Header.Proposer.Verify(bytes, Signature.AsSpan());
    }
}
