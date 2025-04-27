using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types;
using Libplanet.Types.Consensus;

namespace Libplanet.Action.State;

public class ValidatorSetAccount(ITrie trie, int worldVersion)
{
    public static readonly Address ValidatorSetAddress =
        Address.Parse("1000000000000000000000000000000000000000");

    public ITrie Trie { get; } = trie;

    public int WorldVersion { get; } = worldVersion;

    public ImmutableSortedSet<Validator> GetValidatorSet()
    {
        var value = Trie[KeyConverters.ToStateKey(ValidatorSetAddress)];
        return [.. BencodexUtility.ToObjects(value, ModelSerializer.Deserialize<Validator>)];
    }

    public ValidatorSetAccount SetValidatorSet(ImmutableSortedSet<Validator> validatorSet)
    {
        var value = BencodexUtility.ToValue([.. validatorSet], ModelSerializer.Serialize);
        return new ValidatorSetAccount(
            Trie.Set(KeyConverters.ToStateKey(ValidatorSetAddress), value),
            WorldVersion);
    }

    public IAccount AsAccount()
    {
        return new Account(new AccountState(Trie));
    }
}
