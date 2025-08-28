using System.Security.Cryptography;
using Libplanet.State;
using Libplanet.Types;
using static Libplanet.State.SystemAddresses;

namespace Libplanet.Extensions;

public static class BlockchainExtensions
{
    public static ImmutableSortedSet<Validator> GetValidators(this Blockchain @this, int height)
    {
        var stateRootHash = @this.GetStateRootHash(height - 1);
        return @this.GetWorld(stateRootHash).GetValidators();
    }

    public static object GetValue(this Blockchain @this, string name, string key)
        => @this.GetWorld().GetAccount(name).GetValue(key);

    public static object GetValue(this Blockchain @this, Address name, Address key)
        => @this.GetWorld().GetAccount(name).GetValue(key);

    public static object GetValue(this Blockchain @this, int height, string name, string key)
        => @this.GetWorld(height).GetAccount(name).GetValue(key);

    public static object GetValue(this Blockchain @this, int height, Address name, Address key)
        => @this.GetWorld(height).GetAccount(name).GetValue(key);

    public static object GetValue(this Blockchain @this, BlockHash blockHash, string name, string key)
        => @this.GetWorld(blockHash).GetAccount(name).GetValue(key);

    public static object GetValue(this Blockchain @this, BlockHash blockHash, Address name, Address key)
        => @this.GetWorld(blockHash).GetAccount(name).GetValue(key);

    public static object GetValue(this Blockchain @this, HashDigest<SHA256> stateRootHash, string name, string key)
        => @this.GetWorld(stateRootHash).GetAccount(name).GetValue(key);

    public static object GetValue(this Blockchain @this, HashDigest<SHA256> stateRootHash, Address name, Address key)
        => @this.GetWorld(stateRootHash).GetAccount(name).GetValue(key);

    public static object GetSystemValue(this Blockchain @this, string key)
        => @this.GetWorld().GetAccount(SystemAccount).GetValue(key);

    public static object GetSystemValue(this Blockchain @this, Address key)
        => @this.GetWorld().GetAccount(SystemAccount).GetValue(key);

    public static object GetSystemValue(this Blockchain @this, int height, string key)
        => @this.GetWorld(height).GetAccount(SystemAccount).GetValue(key);

    public static object GetSystemValue(this Blockchain @this, int height, Address key)
        => @this.GetWorld(height).GetAccount(SystemAccount).GetValue(key);

    public static object GetSystemValue(this Blockchain @this, BlockHash blockHash, string key)
        => @this.GetWorld(blockHash).GetAccount(SystemAccount).GetValue(key);

    public static object GetSystemValue(this Blockchain @this, BlockHash blockHash, Address key)
        => @this.GetWorld(blockHash).GetAccount(SystemAccount).GetValue(key);

    public static object GetSystemValue(this Blockchain @this, HashDigest<SHA256> stateRootHash, string key)
        => @this.GetWorld(stateRootHash).GetAccount(SystemAccount).GetValue(key);

    public static object GetSystemValue(this Blockchain @this, HashDigest<SHA256> stateRootHash, Address key)
        => @this.GetWorld(stateRootHash).GetAccount(SystemAccount).GetValue(key);
}
