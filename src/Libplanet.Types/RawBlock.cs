using System.Security.Cryptography;
using Libplanet.Serialization;

namespace Libplanet.Types;

[Model(Version = 1)]
public sealed partial record class RawBlock
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

    public Block Sign(PrivateKey privateKey) => new()
    {
        Header = Header,
        Content = Content,
        Signature = CreateSignature(privateKey),
    };

    private ImmutableArray<byte> CreateSignature(PrivateKey privateKey)
    {
        if (Header.Proposer != privateKey.Address)
        {
            throw new ArgumentException(
                $"The given {nameof(privateKey)} does not match the block proposer.", nameof(privateKey));
        }

        var options = new ModelOptions
        {
            IsValidationEnabled = true,
        };
        var message = ModelSerializer.SerializeToBytes(this, options);
        return [.. privateKey.Sign(message)];
    }
}
