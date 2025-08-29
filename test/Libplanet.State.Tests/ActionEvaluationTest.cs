using Libplanet.State.Tests.Actions;
using Libplanet.TestUtilities;
using Libplanet.Types;
using static Libplanet.State.SystemAddresses;

namespace Libplanet.State.Tests;

public class ActionEvaluationTest(ITestOutputHelper output)
{
    [Fact]
    public void Constructor()
    {
        var random = RandomUtility.GetRandom(output);
        var txid = RandomUtility.TxId(random);
        var address = RandomUtility.Address(random);
        var signer = RandomUtility.Signer(random);
        var blockHash = RandomUtility.BlockHash(random);
        var lastCommit = new BlockCommit
        {
            BlockHash = blockHash,
            Votes =
            [
                new VoteMetadata
                {
                    BlockHash = blockHash,
                    Height = 1,
                    Timestamp = DateTimeOffset.UtcNow,
                    Validator = signer.Address,
                    ValidatorPower = BigInteger.One,
                    Type = VoteType.PreCommit,
                }.Sign(signer),
            ],
        };
        var world = new World();
        world = world.SetValue(SystemAccount, address, "item");
        var evaluation = new ActionExecutionInfo
        {
            Action = DumbAction.Create((address, "item")),
            ActionContext = new ActionContext
            {
                Signer = address,
                TxId = txid,
                Proposer = address,
                BlockHeight = 1,
                BlockProtocolVersion = BlockHeader.CurrentProtocolVersion,
                PreviousCommit = lastCommit,
                RandomSeed = 123,
            },
            EnterWorld = new World(),
            LeaveWorld = world,
        };
        var action = (DumbAction)evaluation.Action;

        Assert.Equal(address, action.Append?.At);
        Assert.Equal("item", action.Append?.Item);
        Assert.Equal(address, evaluation.ActionContext.Signer);
        Assert.Equal(txid, evaluation.ActionContext.TxId);
        Assert.Equal(address, evaluation.ActionContext.Proposer);
        Assert.Equal(1, evaluation.ActionContext.BlockHeight);
        Assert.Null(evaluation.EnterWorld.GetValueOrDefault(SystemAccount, address));
        Assert.Equal("item", evaluation.LeaveWorld.GetValue(SystemAccount, address));
        Assert.Equal(lastCommit, evaluation.ActionContext.PreviousCommit);
    }
}
