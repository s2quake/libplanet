using Libplanet.State.Tests.Actions;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Types;
using Xunit.Abstractions;
using static Libplanet.State.SystemAddresses;

namespace Libplanet.State.Tests;

public class ActionEvaluationTest(ITestOutputHelper output)
{
    [Fact]
    public void Constructor()
    {
        var random = RandomUtility.GetRandom(output);
        var txid = RandomUtility.TxId(random);
        var address = new PrivateKey().Address;
        var key = new PrivateKey();
        var hash = RandomUtility.BlockHash(random);
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
                    Type = VoteType.PreCommit,
                }.Sign(key),
            ],
        };
        var world = new World();
        world = world.SetValue(SystemAccount, address, "item");
        var evaluation = new ActionExecutionInfo
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
        Assert.Null(evaluation.InputWorld.GetValueOrDefault(SystemAccount, address));
        Assert.Equal("item", evaluation.OutputWorld.GetValue(SystemAccount, address));
        Assert.Equal(lastCommit, evaluation.InputContext.LastCommit);
    }
}
