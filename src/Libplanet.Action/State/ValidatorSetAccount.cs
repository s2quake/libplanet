using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;

namespace Libplanet.Action.State
{
    /// <summary>
    /// A special "account" for managing <see cref="ImmutableSortedSet<Validator>"/> starting with
    /// <see cref="BlockMetadata.ValidatorSetAccountProtocolVersion"/>.
    /// </summary>
    public class ValidatorSetAccount
    {
        /// <summary>
        /// The <see cref="Address"/> location within the account where a
        /// <see cref="ImmutableSortedSet<Validator>"/> gets stored.
        /// </summary>
        public static readonly Address ValidatorSetAddress =
            Address.Parse("1000000000000000000000000000000000000000");

        public ValidatorSetAccount(ITrie trie, int worldVersion)
        {
            Trie = trie;
            WorldVersion = worldVersion;
        }

        public ITrie Trie { get; }

        public int WorldVersion { get; }

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

        /// <summary>
        /// Converts to an <see cref="IAccount"/> so it can be set to an <see cref="IWorld"/>
        /// using <see cref="IWorld.SetAccount"/>.
        /// </summary>
        /// <returns>An <see cref="IAccount"/> with <see cref="Trie"/> as its
        /// underlying <see cref="IAccountState.Trie"/>.</returns>
        public IAccount AsAccount()
        {
            return new Account(new AccountState(Trie));
        }
    }
}
