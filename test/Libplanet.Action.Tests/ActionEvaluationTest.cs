using System.Collections.Immutable;
using System.Numerics;
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
            var lastCommit = new BlockCommit(
                0,
                0,
                hash,
                new[]
                {
                    new VoteMetadata(
                        0,
                        0,
                        hash,
                        DateTimeOffset.UtcNow,
                        key.PublicKey,
                        BigInteger.One,
                        VoteFlag.PreCommit).Sign(key),
                }.ToImmutableArray());
            IWorld world = new World(MockWorldState.CreateModern());
            world = world.SetAccount(
                ReservedAddresses.LegacyAccount,
                world.GetAccount(ReservedAddresses.LegacyAccount).SetState(address, (Text)"item"));
            var evaluation = new ActionEvaluation(
                DumbAction.Create((address, "item")),
                new ActionContext(
                    address,
                    txid,
                    address,
                    1,
                    Block.CurrentProtocolVersion,
                    lastCommit,
                    new World(MockWorldState.CreateModern()),
                    123,
                    false,
                    maxGasPrice: null),
                world);
            var action = (DumbAction)evaluation.Action;

            Assert.Equal(address, action.Append?.At);
            Assert.Equal("item", action.Append?.Item);
            Assert.Equal(address, evaluation.InputContext.Signer);
            Assert.Equal(txid, evaluation.InputContext.TxId);
            Assert.Equal(address, evaluation.InputContext.Miner);
            Assert.Equal(1, evaluation.InputContext.BlockHeight);
            Assert.Null(
                evaluation.InputContext.PreviousState.GetAccount(
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
