using Libplanet.Types;
using Libplanet.State;
using static Libplanet.State.SystemAddresses;

namespace Libplanet.Extensions;

public static class BlockchainExtensions
{
    public static ImmutableSortedSet<Validator> GetValidators(this Blockchain @this, int height)
    {
        var stateRootHash = @this.GetStateRootHash(height - 1);
        return @this.GetWorld(stateRootHash).GetValidators();
    }

    public static object GetValue(this Blockchain @this, Address address, string key)
        => @this.GetWorld().GetAccount(address).GetValue(key);

    public static object GetSystemValue(this Blockchain @this, string key)
        => @this.GetWorld().GetAccount(SystemAccount).GetValue(key);
    
    public static object GetSystemValue(this Blockchain @this, Address key)
        => @this.GetWorld().GetAccount(SystemAccount).GetValue(key);
}
