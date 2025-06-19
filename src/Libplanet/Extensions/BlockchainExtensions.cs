using Libplanet.Types;
using Libplanet.State;

namespace Libplanet.Extensions;

public static class BlockchainExtensions
{
    public static ImmutableSortedSet<Validator> GetValidators(this Blockchain @this, int height)
    {
        var stateRootHash = @this.GetStateRootHash(height - 1);
        return @this.GetWorld(stateRootHash).GetValidators();
    }
}
