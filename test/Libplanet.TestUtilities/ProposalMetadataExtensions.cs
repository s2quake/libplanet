using Libplanet.Net.Consensus;
using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.TestUtilities;

public static class ProposalMetadataExtensions
{
    public static Proposal SignWithoutValidation(this ProposalMetadata @this, ISigner signer, Block block)
    {
        var bytes = ModelSerializer.SerializeToBytes(@this);
        var signature = signer.Sign(bytes).ToImmutableArray();
        return new Proposal { Metadata = @this, Signature = signature, Block = block };
    }
}
