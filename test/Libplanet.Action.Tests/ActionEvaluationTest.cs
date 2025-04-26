using Bencodex.Types;
using Libplanet.Action.State;
using Libplanet.Action.Tests.Common;
using Libplanet.Crypto;
using Libplanet.Mocks;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Serilog;
using Xunit;
using Xunit.Abstractions;

namespace Libplanet.Action.Tests
{
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
            Address address = new PrivateKey().Address;
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
                        Height = 0,
                        Round = 0,
                        BlockHash = hash,
                        Timestamp = DateTimeOffset.UtcNow,
                        ValidatorPublicKey = key.PublicKey,
                        ValidatorPower = BigInteger.One,
                        Flag = VoteFlag.PreCommit,
                    }.Sign(key),
                ],
            };
            IWorld world = new World(MockWorldState.CreateModern());
            world = world.SetAccount(
                ReservedAddresses.LegacyAccount,
                world.GetAccount(ReservedAddresses.LegacyAccount).SetState(address, (Text)"item"));
            var evaluation = new ActionEvaluation
            {
                Action = DumbAction.Create((address, "item")),
                InputContext = new ActionContext
                {
                    Signer = address,
                    TxId = txid,
                    Miner = address,
                    BlockHeight =  1,
                    BlockProtocolVersion = Block.CurrentProtocolVersion,
                    LastCommit = lastCommit,
                    World = new World(MockWorldState.CreateModern()),
                    RandomSeed = 123,
                },
                OutputState = world,
            };
            var action = (DumbAction)evaluation.Action;

            Assert.Equal(address, action.Append?.At);
            Assert.Equal("item", action.Append?.Item);
            Assert.Equal(address, evaluation.InputContext.Signer);
            Assert.Equal(txid, evaluation.InputContext.TxId);
            Assert.Equal(address, evaluation.InputContext.Miner);
            Assert.Equal(1, evaluation.InputContext.BlockHeight);
            Assert.Null(
                evaluation.InputContext.World.GetAccount(
                    ReservedAddresses.LegacyAccount).GetState(address)
            );
            Assert.Equal(
                (Text)"item",
                evaluation.OutputState.GetAccount(ReservedAddresses.LegacyAccount).GetState(address)
            );
            Assert.Equal(lastCommit, evaluation.InputContext.LastCommit);
        }
    }
}
