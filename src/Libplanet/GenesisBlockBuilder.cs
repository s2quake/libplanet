using Libplanet.State;
using Libplanet.Types;
using System.Security.Cryptography;
using Libplanet.Builtin;

namespace Libplanet;

public sealed record class GenesisBlockBuilder
{
    public required ImmutableSortedSet<Validator> Validators { get; init; }

    public int Height { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public HashDigest<SHA256> StateRootHash { get; init; }

    public ImmutableArray<AccountState> States { get; init; } = [];

    public ImmutableArray<IAction> Actions { get; init; } = [];

    public Block Create(ISigner signer)
    {
        if (Validators.IsEmpty)
        {
            throw new InvalidOperationException("Validators collection is empty.");
        }

        var initialize = new Initialize
        {
            Validators = Validators,
            States = States,
        };
        var tx = new TransactionBuilder
        {
            Timestamp = Timestamp,
            Actions =
            [
                initialize,
                .. Actions,
            ],
        }.Create(signer);
        return new BlockBuilder
        {
            Height = Height,
            Timestamp = Timestamp,
            PreviousBlockCommit = default,
            PreviousStateRootHash = StateRootHash,
            Transactions = [tx],
            Evidence = [],
        }.Create(signer);
    }
}
