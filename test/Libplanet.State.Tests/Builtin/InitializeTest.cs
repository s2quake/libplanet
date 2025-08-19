using Libplanet.State.Builtin;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Types;
using static Libplanet.State.SystemAddresses;

namespace Libplanet.State.Tests.Builtin;

public sealed class InitializeTest(ITestOutputHelper output)
{
    private static readonly ImmutableSortedSet<Validator> _validators =
    [
        new Validator { Address = new PrivateKey().Address },
    ];

    private static readonly ImmutableArray<AccountState> _states =
    [
        new AccountState
        {
            Name = $"{SystemAccount}",
            Values = ImmutableSortedDictionary<string, object>.Empty
                .Add("a", "initial value")
        },
    ];

    [Fact]
    public void Constructor()
    {
        var action = new Initialize
        {
            Validators = _validators,
            States = _states,
        };
        Assert.Equal(_validators, action.Validators);
        Assert.Equal(_states, action.States);
    }

    [Fact]
    public void Execute()
    {
        var random = RandomUtility.GetRandom(output);
        var signer = RandomUtility.Address(random);
        var world = new World();
        var context = new ActionContext
        {
            Signer = signer,
            TxId = RandomUtility.TxId(random),
            Proposer = RandomUtility.Address(random),
            BlockHeight = 0,
            BlockProtocolVersion = BlockHeader.CurrentProtocolVersion,
            RandomSeed = 123,
        };
        var initialize = new Initialize
        {
            States = _states,
            Validators = _validators,
        };

        var nextWorld = ActionUtility.Execute(initialize, world, context);

        Assert.Equal(_validators, nextWorld.GetValidators());
        Assert.Equal(
            _states[0].Values["a"],
            nextWorld.GetValueOrDefault($"{SystemAccount}", "a"));
    }

    [Fact]
    public void ExecuteInNonGenesis()
    {
        var random = RandomUtility.GetRandom(output);
        var signer = RandomUtility.Address(random);
        var world = new World();
        var key = RandomUtility.PrivateKey(random);
        var hash = RandomUtility.BlockHash(random);
        var lastCommit = new BlockCommit
        {
            Height = 0,
            Round = 0,
            BlockHash = hash,
            Votes =
            [
                new VoteMetadata
                {
                    Validator = key.Address,
                    Height = 0,
                    Round = 0,
                    BlockHash = hash,
                    Timestamp = DateTimeOffset.UtcNow,
                    ValidatorPower = BigInteger.One,
                    Type = VoteType.PreCommit,
                }.Sign(key),
            ],
        };
        var context = new ActionContext
        {
            Signer = signer,
            TxId = RandomUtility.TxId(random),
            Proposer = RandomUtility.Address(random),
            BlockHeight = 10,
            BlockProtocolVersion = BlockHeader.CurrentProtocolVersion,
            LastCommit = lastCommit,
            RandomSeed = 123,
        };
        var initialize = new Initialize
        {
            States = _states,
            Validators = _validators,
        };

        Assert.Throws<InvalidOperationException>(
            () => ActionUtility.Execute(initialize, world, context));
    }
}
