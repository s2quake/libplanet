#nullable enable
using System.Diagnostics;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types;
using Libplanet.Types.Consensus;
using static Libplanet.Action.State.KeyConverters;

namespace Libplanet.Action.State
{
    /// <summary>
    /// A default implementation of <see cref="IAccountState"/> interface.
    /// </summary>
    public class AccountState : IAccountState
    {
        private readonly ITrie _trie;
        private readonly ActivitySource _activitySource;

        public AccountState(ITrie trie)
        {
            _trie = trie;
            _activitySource = new ActivitySource("Libplanet.Action.State");
        }

        /// <inheritdoc cref="IAccountState.Trie"/>
        public ITrie Trie => _trie;

        /// <inheritdoc cref="IAccountState.GetState"/>
        public IValue? GetState(Address address)
        {
            using Activity? a = _activitySource
                .StartActivity(ActivityKind.Internal)?
                .AddTag("Address", address.ToString());
            return Trie[ToStateKey(address)];
        }

        /// <inheritdoc cref="IAccountState.GetStates"/>
        public IReadOnlyList<IValue?> GetStates(IReadOnlyList<Address> addresses) =>
            addresses.Select(address => GetState(address)).ToList();

        /// <inheritdoc cref="IAccountState.GetValidatorSet"/>
        public ImmutableSortedSet<Validator> GetValidatorSet()
        {
            var value = Trie[ValidatorSetKey];
            return [.. BencodexUtility.ToObjects(value, ModelSerializer.Deserialize<Validator>)];
        }
    }
}
