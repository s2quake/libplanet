using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Action.Tests.Common;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Tests.Store;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;
using Nito.AsyncEx;
using Serilog;
using Xunit.Abstractions;

namespace Libplanet.Net.Tests.Consensus
{
    public class ContextNonProposerTest
    {
        private const int Timeout = 30000;
        private readonly ILogger _logger;

        public ContextNonProposerTest(ITestOutputHelper output)
        {
            const string outputTemplate =
                "{Timestamp:HH:mm:ss:ffffffZ} - {Message} {Exception}";
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.TestOutput(output, outputTemplate: outputTemplate)
                .CreateLogger()
                .ForContext<ContextNonProposerTest>();

            _logger = Log.ForContext<ContextNonProposerTest>();
        }

        [Fact(Timeout = Timeout)]
        public async Task EnterPreVoteBlockOneThird()
        {
            var (blockChain, context) = TestUtils.CreateDummyContext(
                privateKey: TestUtils.PrivateKeys[0]);

            var block = blockChain.ProposeBlock(TestUtils.PrivateKeys[1]);
            var stateChangedToRoundOnePreVote = new AsyncAutoResetEvent();
            context.StateChanged += (_, eventArgs) =>
            {
                if (eventArgs.Round == 1 && eventArgs.Step == ConsensusStep.PreVote)
                {
                    stateChangedToRoundOnePreVote.Set();
                }
            };

            context.Start();
            context.ProduceMessage(
                new ConsensusPreVoteMsg(
                    TestUtils.CreateVote(
                        TestUtils.PrivateKeys[2],
                        TestUtils.Validators[2].Power,
                        1,
                        1,
                        hash: block.Hash,
                        flag: VoteFlag.PreVote)));
            context.ProduceMessage(
                new ConsensusPreVoteMsg(
                    TestUtils.CreateVote(
                        TestUtils.PrivateKeys[3],
                        TestUtils.Validators[3].Power,
                        1,
                        1,
                        hash: block.Hash,
                        flag: VoteFlag.PreVote)));

            // Wait for round 1 prevote step.
            await stateChangedToRoundOnePreVote.WaitAsync();
            Assert.Equal(ConsensusStep.PreVote, context.Step);
            Assert.Equal(1, context.Height);
            Assert.Equal(1, context.Round);
        }

        [Fact(Timeout = Timeout)]
        public async Task EnterPreCommitBlockTwoThird()
        {
            var stepChangedToPreCommit = new AsyncAutoResetEvent();
            ConsensusPreCommitMsg? preCommit = null;
            var preCommitSent = new AsyncAutoResetEvent();
            var (blockChain, context) = TestUtils.CreateDummyContext(
                privateKey: TestUtils.PrivateKeys[0]);

            var block = blockChain.ProposeBlock(TestUtils.PrivateKeys[1]);

            context.StateChanged += (_, eventArgs) =>
            {
                if (eventArgs.Step == ConsensusStep.PreCommit)
                {
                    stepChangedToPreCommit.Set();
                }
            };
            context.MessageToPublish += (_, message) =>
            {
                if (message is ConsensusPreCommitMsg preCommitMsg)
                {
                    preCommit = preCommitMsg;
                    preCommitSent.Set();
                }
            };

            context.Start();
            context.ProduceMessage(
                TestUtils.CreateConsensusPropose(block, TestUtils.PrivateKeys[1]));

            context.ProduceMessage(
                new ConsensusPreVoteMsg(
                    TestUtils.CreateVote(
                        TestUtils.PrivateKeys[1],
                        TestUtils.Validators[1].Power,
                        1,
                        0,
                        hash: block.Hash,
                        VoteFlag.PreVote)));
            context.ProduceMessage(
                new ConsensusPreVoteMsg(
                    TestUtils.CreateVote(
                        TestUtils.PrivateKeys[2],
                        TestUtils.Validators[2].Power,
                        1,
                        0,
                        hash: block.Hash,
                        VoteFlag.PreVote)));
            context.ProduceMessage(
                new ConsensusPreVoteMsg(
                    TestUtils.CreateVote(
                        TestUtils.PrivateKeys[3],
                        TestUtils.Validators[3].Power,
                        1,
                        0,
                        hash: block.Hash,
                        VoteFlag.PreVote)));

            await Task.WhenAll(preCommitSent.WaitAsync(), stepChangedToPreCommit.WaitAsync());
            Assert.Equal(block.Hash, preCommit?.BlockHash);
            Assert.Equal(ConsensusStep.PreCommit, context.Step);
            Assert.Equal(1, context.Height);
            Assert.Equal(0, context.Round);

            var json =
                JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(context.ToString())
                    ?? throw new NullReferenceException("Failed to deserialize context");

            Assert.Equal(0, json["locked_round"].GetInt64());
            Assert.Equal(0, json["valid_round"].GetInt64());
            Assert.Equal(block.Hash.ToString(), json["locked_value"].GetString());
            Assert.Equal(block.Hash.ToString(), json["valid_value"].GetString());
        }

        [Fact(Timeout = Timeout)]
        public async void EnterPreCommitNilTwoThird()
        {
            var stepChangedToPreCommit = new AsyncAutoResetEvent();
            var preCommitSent = new AsyncAutoResetEvent();

            var (blockChain, context) = TestUtils.CreateDummyContext(
                privateKey: TestUtils.PrivateKeys[0]);

            var key = new PrivateKey();
            var invalidBlock = blockChain.EvaluateAndSign(
                RawBlock.Propose(
                    new BlockMetadata
                    {
                        Height = blockChain.Tip.Height + 1,
                        Timestamp = DateTimeOffset.UtcNow,
                        PublicKey = key.PublicKey,
                        PreviousHash = blockChain.Tip.Hash,
                    }),
                key);

            context.StateChanged += (_, eventArgs) =>
            {
                if (eventArgs.Step == ConsensusStep.PreCommit)
                {
                    stepChangedToPreCommit.Set();
                }
            };
            context.MessageToPublish += (_, message) =>
            {
                if (message is ConsensusPreCommitMsg preCommitMsg &&
                    preCommitMsg.BlockHash.Equals(default))
                {
                    preCommitSent.Set();
                }
            };

            context.Start();
            context.ProduceMessage(
                TestUtils.CreateConsensusPropose(invalidBlock, TestUtils.PrivateKeys[1]));
            context.ProduceMessage(
                new ConsensusPreVoteMsg(
                    TestUtils.CreateVote(
                        TestUtils.PrivateKeys[1],
                        TestUtils.Validators[1].Power,
                        1,
                        0,
                        hash: default,
                        VoteFlag.PreVote)));
            context.ProduceMessage(
                new ConsensusPreVoteMsg(
                    TestUtils.CreateVote(
                        TestUtils.PrivateKeys[2],
                        TestUtils.Validators[2].Power,
                        1,
                        0,
                        hash: default,
                        VoteFlag.PreVote)));
            context.ProduceMessage(
                new ConsensusPreVoteMsg(
                    TestUtils.CreateVote(
                        TestUtils.PrivateKeys[3],
                        TestUtils.Validators[3].Power,
                        1,
                        0,
                        hash: default,
                        VoteFlag.PreVote)));

            await Task.WhenAll(preCommitSent.WaitAsync(), stepChangedToPreCommit.WaitAsync());
            Assert.Equal(ConsensusStep.PreCommit, context.Step);
            Assert.Equal(1, context.Height);
            Assert.Equal(0, context.Round);
        }

        [Fact(Timeout = Timeout)]
        public async Task EnterPreVoteNilOnInvalidBlockHeader()
        {
            var stepChangedToPreVote = new AsyncAutoResetEvent();
            var timeoutProcessed = false;
            var nilPreVoteSent = new AsyncAutoResetEvent();

            var (blockChain, context) = TestUtils.CreateDummyContext(
                privateKey: TestUtils.PrivateKeys[0]);
            context.StateChanged += (_, eventArgs) =>
            {
                if (eventArgs.Step == ConsensusStep.PreVote)
                {
                    stepChangedToPreVote.Set();
                }
            };
            context.TimeoutProcessed += (_, __) =>
            {
                timeoutProcessed = true;
            };
            context.MessageToPublish += (_, message) =>
            {
                if (message is ConsensusPreVoteMsg vote && vote.PreVote.BlockHash.Equals(default))
                {
                    nilPreVoteSent.Set();
                }
            };

            // 1. ProtocolVersion should be matched.
            // 2. Index should be increased monotonically.
            // 3. Timestamp should be increased monotonically.
            // 4. PreviousHash should be matched with Tip hash.
            var invalidBlock = blockChain.EvaluateAndSign(
                RawBlock.Propose(
                    new BlockMetadata
                    {
                        ProtocolVersion = BlockMetadata.CurrentProtocolVersion,
                        Height = blockChain.Tip.Height + 2,
                        Timestamp = blockChain.Tip.Timestamp.Subtract(TimeSpan.FromSeconds(1)),
                        Miner = TestUtils.PrivateKeys[1].Address,
                        PublicKey = TestUtils.PrivateKeys[1].PublicKey,
                        PreviousHash = blockChain.Tip.Hash,
                    }),
                TestUtils.PrivateKeys[1]);

            context.Start();
            context.ProduceMessage(
                TestUtils.CreateConsensusPropose(
                    invalidBlock, TestUtils.PrivateKeys[1]));

            await Task.WhenAll(nilPreVoteSent.WaitAsync(), stepChangedToPreVote.WaitAsync());
            Assert.False(timeoutProcessed); // Check step transition isn't by timeout.
            Assert.Equal(ConsensusStep.PreVote, context.Step);
            Assert.Equal(1, context.Height);
            Assert.Equal(0, context.Round);
        }

        [Fact(Timeout = Timeout)]
        public async Task EnterPreVoteNilOnInvalidBlockContent()
        {
            // NOTE: This test does not check tx nonces, different state root hash.
            var stepChangedToPreVote = new AsyncAutoResetEvent();
            var timeoutProcessed = false;
            var nilPreVoteSent = new AsyncAutoResetEvent();
            var invalidKey = new PrivateKey();
            var policy = new BlockPolicy(
                new PolicyActions
                {
                    EndBlockActions = [new MinerReward(1)],
                },
                getMaxTransactionsBytes: _ => 50 * 1024,
                validateNextBlockTx: IsSignerValid);

            InvalidOperationException? IsSignerValid(
                BlockChain chain, Transaction tx)
            {
                var validAddress = TestUtils.PrivateKeys[1].Address;
                return tx.Signer.Equals(validAddress)
                    ? null
                    : new InvalidOperationException("invalid signer");
            }

            var (blockChain, context) = TestUtils.CreateDummyContext(
                policy: policy,
                privateKey: TestUtils.PrivateKeys[0]);
            context.StateChanged += (_, eventArgs) =>
            {
                if (eventArgs.Step == ConsensusStep.PreVote)
                {
                    stepChangedToPreVote.Set();
                }
            };
            context.TimeoutProcessed += (_, __) =>
            {
                timeoutProcessed = true;
            };
            context.MessageToPublish += (_, message) =>
            {
                if (message is ConsensusPreVoteMsg vote && vote.PreVote.BlockHash.Equals(default))
                {
                    nilPreVoteSent.Set();
                }
            };

            var diffPolicyBlockChain =
                TestUtils.CreateDummyBlockChain(
                    policy, new SingleActionLoader<DumbAction>(), blockChain.Genesis);

            var invalidTx = diffPolicyBlockChain.MakeTransaction(invalidKey, new DumbAction[] { });

            Block invalidBlock = diffPolicyBlockChain.EvaluateAndSign(
                Libplanet.Tests.TestUtils.ProposeNext(
                    blockChain.Genesis,
                    new[] { invalidTx },
                    miner: TestUtils.PrivateKeys[1].PublicKey,
                    blockInterval: TimeSpan.FromSeconds(10)),
                TestUtils.PrivateKeys[1]);

            context.Start();
            context.ProduceMessage(
                TestUtils.CreateConsensusPropose(
                    invalidBlock,
                    TestUtils.PrivateKeys[1]));

            await Task.WhenAll(nilPreVoteSent.WaitAsync(), stepChangedToPreVote.WaitAsync());
            Assert.False(timeoutProcessed); // Check step transition isn't by timeout.
            Assert.Equal(ConsensusStep.PreVote, context.Step);
            Assert.Equal(1, context.Height);
            Assert.Equal(0, context.Round);
        }

        [Fact(Timeout = Timeout)]
        public async Task EnterPreVoteNilOnInvalidAction()
        {
            // NOTE: This test does not check tx nonces, different state root hash.
            var stepChangedToPreVote = new AsyncAutoResetEvent();
            var timeoutProcessed = false;
            var nilPreVoteSent = new AsyncAutoResetEvent();
            var nilPreCommitSent = new AsyncAutoResetEvent();
            var txSigner = new PrivateKey();
            var policy = new BlockPolicy(
                new PolicyActions
                {
                    EndBlockActions = [new MinerReward(1)],
                },
                getMaxTransactionsBytes: _ => 50 * 1024);

            var (blockChain, context) = TestUtils.CreateDummyContext(
                policy: policy,
                privateKey: TestUtils.PrivateKeys[0]);
            context.StateChanged += (_, eventArgs) =>
            {
                if (eventArgs.Step == ConsensusStep.PreVote)
                {
                    stepChangedToPreVote.Set();
                }
            };
            context.TimeoutProcessed += (_, __) =>
            {
                timeoutProcessed = true;
            };
            context.MessageToPublish += (_, message) =>
            {
                if (message is ConsensusPreVoteMsg vote && vote.PreVote.BlockHash.Equals(default))
                {
                    nilPreVoteSent.Set();
                }
                else if (
                    message is ConsensusPreCommitMsg commit &&
                    commit.PreCommit.BlockHash.Equals(default))
                {
                    nilPreCommitSent.Set();
                }
            };

            using var fx = new MemoryStoreFixture(policy.PolicyActions);

            var unsignedInvalidTx = new UnsignedTx
            {
                Invoice = new TxInvoice
                {
                    GenesisHash = blockChain.Genesis.Hash,
                    Timestamp = DateTimeOffset.UtcNow,
                    Actions = [List.Empty.Add(new Text("Foo"))], // Invalid action
                },
                SigningMetadata = new TxSigningMetadata
                {
                    Signer = txSigner.Address,
                },
            };
            var invalidTx = new Transaction
            {
                UnsignedTx = unsignedInvalidTx,
                Signature = unsignedInvalidTx.CreateSignature(txSigner),
            };
            var txs = new[] { invalidTx };
            var evs = Array.Empty<EvidenceBase>();

            var metadata = new BlockMetadata
            {
                Height = 1L,
                Timestamp = DateTimeOffset.UtcNow,
                PublicKey = TestUtils.PrivateKeys[1].PublicKey,
                PreviousHash = blockChain.Genesis.Hash,
                TxHash = BlockContent.DeriveTxHash(txs),
            };
            var preEval = RawBlock.Propose(
                metadata,
                new BlockContent
                {
                    Transactions = [.. txs],
                    Evidence = [.. evs],
                });
            var invalidBlock = preEval.Sign(
                TestUtils.PrivateKeys[1],
                HashDigest<SHA256>.DeriveFrom(TestUtils.GetRandomBytes(1024)));

            context.Start();
            context.ProduceMessage(
                TestUtils.CreateConsensusPropose(
                    invalidBlock,
                    TestUtils.PrivateKeys[1]));
            await Task.WhenAll(nilPreVoteSent.WaitAsync(), stepChangedToPreVote.WaitAsync());
            Assert.False(timeoutProcessed); // Check step transition isn't by timeout.
            Assert.Equal(ConsensusStep.PreVote, context.Step);
            Assert.Equal(1, context.Height);
            Assert.Equal(0, context.Round);

            context.ProduceMessage(
                new ConsensusPreVoteMsg(
                    TestUtils.CreateVote(
                        TestUtils.PrivateKeys[1],
                        TestUtils.Validators[1].Power,
                        1,
                        0,
                        invalidBlock.Hash,
                        VoteFlag.PreVote)));
            context.ProduceMessage(
                new ConsensusPreVoteMsg(
                    TestUtils.CreateVote(
                        TestUtils.PrivateKeys[2],
                        TestUtils.Validators[2].Power,
                        1,
                        0,
                        default,
                        VoteFlag.PreVote)));
            context.ProduceMessage(
                new ConsensusPreVoteMsg(
                    TestUtils.CreateVote(
                        TestUtils.PrivateKeys[3],
                        TestUtils.Validators[3].Power,
                        1,
                        0,
                        default,
                        VoteFlag.PreVote)));
            await nilPreCommitSent.WaitAsync();
            Assert.Equal(ConsensusStep.PreCommit, context.Step);
        }

        [Fact(Timeout = Timeout)]
        public async Task EnterPreVoteNilOneThird()
        {
            var (blockChain, context) = TestUtils.CreateDummyContext(
                privateKey: TestUtils.PrivateKeys[0]);

            var block = blockChain.ProposeBlock(TestUtils.PrivateKeys[1]);
            var stepChangedToRoundOnePreVote = new AsyncAutoResetEvent();
            context.StateChanged += (_, eventArgs) =>
            {
                if (eventArgs.Round == 1 && eventArgs.Step == ConsensusStep.PreVote)
                {
                    stepChangedToRoundOnePreVote.Set();
                }
            };
            context.Start();

            context.ProduceMessage(
                new ConsensusPreVoteMsg(
                    TestUtils.CreateVote(
                        TestUtils.PrivateKeys[2],
                        TestUtils.Validators[2].Power,
                        1,
                        1,
                        hash: default,
                        flag: VoteFlag.PreVote)));
            context.ProduceMessage(
                new ConsensusPreVoteMsg(
                    TestUtils.CreateVote(
                        TestUtils.PrivateKeys[3],
                        TestUtils.Validators[3].Power,
                        1,
                        1,
                        hash: default,
                        flag: VoteFlag.PreVote)));

            await stepChangedToRoundOnePreVote.WaitAsync();
            Assert.Equal(ConsensusStep.PreVote, context.Step);
            Assert.Equal(1, context.Height);
            Assert.Equal(1, context.Round);
        }

        [Fact(Timeout = Timeout)]
        public async void TimeoutPropose()
        {
            var stepChangedToPreVote = new AsyncAutoResetEvent();
            var preVoteSent = new AsyncAutoResetEvent();

            var (_, context) = TestUtils.CreateDummyContext(
                privateKey: TestUtils.PrivateKeys[0],
                contextOption: new ContextOption(proposeTimeoutBase: 1_000));

            context.StateChanged += (_, eventArgs) =>
            {
                if (eventArgs.Step == ConsensusStep.PreVote)
                {
                    stepChangedToPreVote.Set();
                }
            };
            context.MessageToPublish += (_, message) =>
            {
                if (message is ConsensusPreVoteMsg)
                {
                    preVoteSent.Set();
                }
            };

            context.Start();
            await Task.WhenAll(preVoteSent.WaitAsync(), stepChangedToPreVote.WaitAsync());
            Assert.Equal(ConsensusStep.PreVote, context.Step);
            Assert.Equal(1, context.Height);
            Assert.Equal(0, context.Round);
        }

        [Fact(Timeout = Timeout)]
        public async Task UponRulesCheckAfterTimeout()
        {
            var (blockChain, context) = TestUtils.CreateDummyContext(
                privateKey: TestUtils.PrivateKeys[0],
                contextOption: new ContextOption(
                    preVoteTimeoutBase: 1_000,
                    preCommitTimeoutBase: 1_000));

            var block1 = blockChain.ProposeBlock(TestUtils.PrivateKeys[1]);
            var block2 = blockChain.ProposeBlock(TestUtils.PrivateKeys[2]);
            var roundOneStepChangedToPreVote = new AsyncAutoResetEvent();
            context.StateChanged += (_, eventArgs) =>
            {
                if (eventArgs.Round == 1 && eventArgs.Step == ConsensusStep.PreVote)
                {
                    roundOneStepChangedToPreVote.Set();
                }
            };

            // Push round 0 and round 1 proposes.
            context.ProduceMessage(
                TestUtils.CreateConsensusPropose(
                    block1, TestUtils.PrivateKeys[1], round: 0));
            context.ProduceMessage(
                TestUtils.CreateConsensusPropose(
                    block2, TestUtils.PrivateKeys[2], round: 1));

            // Two additional votes should be enough to trigger prevote timeout timer.
            context.ProduceMessage(
                new ConsensusPreVoteMsg(
                    TestUtils.CreateVote(
                        TestUtils.PrivateKeys[2],
                        TestUtils.Validators[2].Power,
                        1,
                        0,
                        hash: default,
                        flag: VoteFlag.PreVote)));
            context.ProduceMessage(
                new ConsensusPreVoteMsg(
                    TestUtils.CreateVote(
                        TestUtils.PrivateKeys[3],
                        TestUtils.Validators[3].Power,
                        1,
                        0,
                        hash: default,
                        flag: VoteFlag.PreVote)));

            // Two additional votes should be enough to trigger precommit timeout timer.
            context.ProduceMessage(
                new ConsensusPreCommitMsg(
                    TestUtils.CreateVote(
                        TestUtils.PrivateKeys[2],
                        TestUtils.Validators[2].Power,
                        1,
                        0,
                        hash: default,
                        flag: VoteFlag.PreCommit)));
            context.ProduceMessage(
                new ConsensusPreCommitMsg(
                    TestUtils.CreateVote(
                        TestUtils.PrivateKeys[3],
                        TestUtils.Validators[3].Power,
                        1,
                        0,
                        hash: default,
                        flag: VoteFlag.PreCommit)));

            context.Start();

            // Round 0 Propose -> Round 0 PreVote (due to Round 0 Propose message) ->
            // PreVote timeout start (due to PreVote messages) ->
            // PreVote timeout end -> Round 0 PreCommit ->
            // PreCommit timeout start (due to state mutation check and PreCommit messages) ->
            // PreCommit timeout end -> Round 1 Propose ->
            // Round 1 PreVote (due to state mutation check and Round 1 Propose message)
            await roundOneStepChangedToPreVote.WaitAsync();
            Assert.Equal(1, context.Height);
            Assert.Equal(1, context.Round);
            Assert.Equal(ConsensusStep.PreVote, context.Step);
        }

        [Fact(Timeout = Timeout)]
        public async Task TimeoutPreVote()
        {
            var (blockChain, context) = TestUtils.CreateDummyContext(
                privateKey: TestUtils.PrivateKeys[0],
                contextOption: new ContextOption(preVoteTimeoutBase: 1_000));

            var block = blockChain.ProposeBlock(TestUtils.PrivateKeys[1]);
            var timeoutProcessed = new AsyncAutoResetEvent();
            context.TimeoutProcessed += (_, __) => timeoutProcessed.Set();
            context.Start();

            context.ProduceMessage(
                TestUtils.CreateConsensusPropose(
                    block, TestUtils.PrivateKeys[1], round: 0));

            context.ProduceMessage(
                new ConsensusPreVoteMsg(
                    TestUtils.CreateVote(
                        TestUtils.PrivateKeys[1],
                        TestUtils.Validators[1].Power,
                        1,
                        0,
                        hash: block.Hash,
                        flag: VoteFlag.PreVote)));
            context.ProduceMessage(
                new ConsensusPreVoteMsg(
                    TestUtils.CreateVote(
                        TestUtils.PrivateKeys[2],
                        TestUtils.Validators[2].Power,
                        1,
                        0,
                        hash: default,
                        flag: VoteFlag.PreVote)));
            context.ProduceMessage(
                new ConsensusPreVoteMsg(
                    TestUtils.CreateVote(
                        TestUtils.PrivateKeys[3],
                        TestUtils.Validators[3].Power,
                        1,
                        0,
                        hash: default,
                        flag: VoteFlag.PreVote)));

            // Wait for timeout.
            await timeoutProcessed.WaitAsync();
            Assert.Equal(ConsensusStep.PreCommit, context.Step);
            Assert.Equal(1, context.Height);
            Assert.Equal(0, context.Round);
        }

        [Fact(Timeout = Timeout)]
        public async Task TimeoutPreCommit()
        {
            var (blockChain, context) = TestUtils.CreateDummyContext(
                privateKey: TestUtils.PrivateKeys[0],
                contextOption: new ContextOption(preCommitTimeoutBase: 1_000));

            var block = blockChain.ProposeBlock(TestUtils.PrivateKeys[1]);
            var timeoutProcessed = new AsyncAutoResetEvent();
            context.TimeoutProcessed += (_, __) => timeoutProcessed.Set();
            context.Start();

            context.ProduceMessage(
                TestUtils.CreateConsensusPropose(
                    block, TestUtils.PrivateKeys[1], round: 0));

            context.ProduceMessage(
                new ConsensusPreCommitMsg(
                    TestUtils.CreateVote(
                        TestUtils.PrivateKeys[1],
                        TestUtils.Validators[1].Power,
                        1,
                        0,
                        hash: block.Hash,
                        flag: VoteFlag.PreCommit)));
            context.ProduceMessage(
                new ConsensusPreCommitMsg(
                    TestUtils.CreateVote(
                        TestUtils.PrivateKeys[2],
                        TestUtils.Validators[2].Power,
                        1,
                        0,
                        hash: default,
                        flag: VoteFlag.PreCommit)));
            context.ProduceMessage(
                new ConsensusPreCommitMsg(
                    TestUtils.CreateVote(
                        TestUtils.PrivateKeys[3],
                        TestUtils.Validators[3].Power,
                        1,
                        0,
                        hash: default,
                        flag: VoteFlag.PreCommit)));

            // Wait for timeout.
            await timeoutProcessed.WaitAsync();
            Assert.Equal(ConsensusStep.Propose, context.Step);
            Assert.Equal(1, context.Height);
            Assert.Equal(1, context.Round);
        }
    }
}
