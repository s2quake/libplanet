using Bencodex.Types;
using Libplanet.Action.State;
using Libplanet.Action.Sys;
using Libplanet.Crypto;
using Libplanet.Mocks;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Xunit;

namespace Libplanet.Action.Tests.Sys;

public class InitializeTest
{
    private static readonly ImmutableSortedSet<Validator> _validators = ImmutableSortedSet.Create(
    [
        new Validator(new PrivateKey().PublicKey, BigInteger.One),
    ]);

    private static readonly ImmutableDictionary<Address, IValue>
        _states =
        new Dictionary<Address, IValue>
        {
            [default] = (Text)"initial value",
        }.ToImmutableDictionary();

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
        var prevState = new World(MockWorldState.CreateModern());
        BlockHash genesisHash = random.NextBlockHash();
        var context = new ActionContext
        {
            Signer = signer,
            TxId = random.NextTxId(),
            Miner = random.NextAddress(),
            BlockHeight = 0,
            BlockProtocolVersion = Block.CurrentProtocolVersion,
            LastCommit = null,
            World = prevState,
            RandomSeed = 123,
            MaxGasPrice = null,
        };
        var initialize = new Initialize
        {
            States = _states,
            Validators = _validators,
        };

        var nextState = ((IAction)initialize).Execute(context);

        Assert.Equal(_validators, nextState.GetValidatorSet());
        Assert.Equal(
            _states[default],
            nextState.GetAccount(ReservedAddresses.LegacyAccount).GetState(default));
    }

    [Fact]
    public void ExecuteInNonGenesis()
    {
        var random = new System.Random();
        Address signer = random.NextAddress();
        var prevState = new World(MockWorldState.CreateModern());
        BlockHash genesisHash = random.NextBlockHash();
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
                    ValidatorPublicKey = key.PublicKey,
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
            Miner = random.NextAddress(),
            BlockHeight = 10,
            BlockProtocolVersion = Block.CurrentProtocolVersion,
            LastCommit = lastCommit,
            World = prevState,
            RandomSeed = 123,
            MaxGasPrice = null,
        };
        var initialize = new Initialize
        {
            States = _states,
            Validators = _validators,
        };

        Assert.Throws<InvalidOperationException>(
            () => ((IAction)initialize).Execute(context));
    }
}
