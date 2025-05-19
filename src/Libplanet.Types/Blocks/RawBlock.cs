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
    public BlockContent Content { get; init; } = new();

    public HashDigest<SHA256> Hash => HashDigest<SHA256>.Create(ModelSerializer.SerializeToBytes(this));

    public static explicit operator RawBlock(Block block) => new()
    {
        Header = block.Header,
        Content = block.Content,
    };

    public Block Sign(PrivateKey privateKey, HashDigest<SHA256> stateRootHash) => new()
    {
        Header = Header,
        Content = Content,
        StateRootHash = stateRootHash,
        Signature = MakeSignature(privateKey, stateRootHash),
    };

    private static ImmutableArray<byte> MakeSignature(PrivateKey privateKey, HashDigest<SHA256> stateRootHash)
    {
        var msg = ModelSerializer.SerializeToBytes(stateRootHash);
        var sig = privateKey.Sign(msg);
        return ImmutableArray.Create(sig);
    }
}
