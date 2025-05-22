using Libplanet.Action.State;
using Libplanet.Action.Tests.Common;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;
using Serilog;
using Xunit.Abstractions;
using static Libplanet.Action.State.ReservedAddresses;

namespace Libplanet.Action.Tests;

public class ActionEvaluationTest
{
    private readonly ILogger _logger;

    public ActionEvaluationTest(ITestOutputHelper output)
    {
        Log.Logger = _logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.WithThreadId()
            .WriteTo.TestOutput(output)
            .CreateLogger()
            .ForContext<ActionEvaluationTest>();
    }

    [Fact]
    public void Constructor()
    {
        var random = new System.Random();
        var txid = random.NextTxId();
        var address = new PrivateKey().Address;
        var key = new PrivateKey();
        var hash = random.NextBlockHash();
        var lastCommit = new BlockCommit
        {
            BlockHash = hash,
            Votes =
            [
                new VoteMetadata
                {
                    BlockHash = hash,
                    Timestamp = DateTimeOffset.UtcNow,
                    Validator = key.Address,
                    ValidatorPower = BigInteger.One,
                    Flag = VoteFlag.PreCommit,
                }.Sign(key),
            ],
        };
        var world = new World();
        world = world.SetValue(LegacyAccount, address, "item");
        var evaluation = new ActionEvaluation
        {
            Action = DumbAction.Create((address, "item")),
            InputContext = new ActionContext
            {
                Signer = address,
                TxId = txid,
                Proposer = address,
                BlockHeight = 1,
                BlockProtocolVersion = BlockHeader.CurrentProtocolVersion,
                LastCommit = lastCommit,
                RandomSeed = 123,
            },
            InputWorld = new World(),
            OutputWorld = world,
        };
        var action = (DumbAction)evaluation.Action;

        Assert.Equal(address, action.Append?.At);
        Assert.Equal("item", action.Append?.Item);
        Assert.Equal(address, evaluation.InputContext.Signer);
        Assert.Equal(txid, evaluation.InputContext.TxId);
        Assert.Equal(address, evaluation.InputContext.Proposer);
        Assert.Equal(1, evaluation.InputContext.BlockHeight);
        Assert.Null(evaluation.InputWorld.GetValueOrDefault(LegacyAccount, address));
        Assert.Equal("item", evaluation.OutputWorld.GetValue(LegacyAccount, address));
        Assert.Equal(lastCommit, evaluation.InputContext.LastCommit);
    }
}
