using Libplanet.Serialization;

namespace Libplanet.Types;

[Model(Version = 1, TypeName = "rblk")]
public sealed partial record class RawBlock
{
    [Property(0)]
    public required BlockHeader Header { get; init; }

    [Property(1)]
    public BlockContent Content { get; init; } = new();

    public static explicit operator RawBlock(Block block) => new()
    {
        Header = block.Header,
        Content = block.Content,
    };

    public Block Sign(ISigner signer) => new()
    {
        Header = Header,
        Content = Content,
        Signature = CreateSignature(signer),
    };

    private ImmutableArray<byte> CreateSignature(ISigner signer)
    {
        if (Header.Proposer != signer.Address)
        {
            throw new ArgumentException(
                $"The given {nameof(signer)} does not match the block proposer.", nameof(signer));
        }

        var options = new ModelOptions
        {
            IsValidationEnabled = true,
        };
        var message = ModelSerializer.SerializeToBytes(this, options);
        return [.. signer.Sign(message)];
    }
}
