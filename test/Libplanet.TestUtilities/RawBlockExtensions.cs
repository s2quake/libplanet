using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.TestUtilities;

public static class RawBlockExtensions
{
    public static Block SignWithoutValidation(this RawBlock @this, ISigner signer)
    {
        return new Block
        {
            Header = @this.Header,
            Content = @this.Content,
            Signature = CreateSignature(@this, signer),
        };
    }

    private static ImmutableArray<byte> CreateSignature(RawBlock rawBlock, ISigner signer)
    {
        if (rawBlock.Header.Proposer != signer.Address)
        {
            throw new ArgumentException(
                $"The given {nameof(signer)} does not match the block proposer.", nameof(signer));
        }

        var message = ModelSerializer.SerializeToBytes(rawBlock);
        return [.. signer.Sign(message)];
    }
}
