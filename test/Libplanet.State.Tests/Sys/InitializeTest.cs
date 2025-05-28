using Libplanet.State.Builtin;
using Libplanet.Types;
using static Libplanet.State.SystemAddresses;

namespace Libplanet.State.Tests.Sys;

public class InitializeTest
{
    private static readonly ImmutableSortedSet<Validator> _validators = ImmutableSortedSet.Create(
    [
        new Validator { Address = new PrivateKey().Address },
    ]);

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
        var random = new System.Random();
        Address signer = random.NextAddress();
        var world = new World();
        BlockHash genesisHash = random.NextBlockHash();
        var context = new ActionContext
        {
            Signer = signer,
            TxId = random.NextTxId(),
            Proposer = random.NextAddress(),
            BlockHeight = 0,
            BlockProtocolVersion = BlockHeader.CurrentProtocolVersion,
            RandomSeed = 123,
        };
        var initialize = new Initialize
        {
            States = _states,
            Validators = _validators,
        };

        var nextWorld = TestUtils.ExecuteAction(initialize, world, context);

        Assert.Equal(_validators, nextWorld.GetValidators());
        Assert.Equal(
            _states[default],
            nextWorld.GetValueOrDefault(SystemAccount, default));
    }

    [Fact]
    public void ExecuteInNonGenesis()
    {
        var random = new System.Random();
        Address signer = random.NextAddress();
        var world = new World();
        var key = new PrivateKey();
        var hash = random.NextBlockHash();
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
                    Flag = VoteFlag.PreCommit,
                }.Sign(key),
            ],
        };
        var context = new ActionContext
        {
            Signer = signer,
            TxId = random.NextTxId(),
            Proposer = random.NextAddress(),
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
            () => TestUtils.ExecuteAction(initialize, world, context));
    }
}
