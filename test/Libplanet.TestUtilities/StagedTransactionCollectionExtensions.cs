using Libplanet.Types;

namespace Libplanet.TestUtilities;

public static class StagedTransactionCollectionExtensions
{
    public static Transaction Add(this StagedTransactionCollection @this, ISigner signer)
        => @this.Add(signer, new());
}
