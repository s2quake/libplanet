using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Tests.Store;
using Libplanet.Types;
using Nito.AsyncEx;
using Serilog;
using Xunit.Abstractions;
using Libplanet.Types.Tests;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Extensions;

namespace Libplanet.Net.Tests.Consensus;

public class ContextNonProposerTest(ITestOutputHelper output)
{
    private const int Timeout = 30000;

    [Fact(Timeout = Timeout)]
    public async Task EnterPreVoteBlockOneThird()
    {
        var blockchain = TestUtils.CreateBlockchain();
        await using var consensus = TestUtils.CreateConsensus(
            blockchain: blockchain,
            privateKey: TestUtils.PrivateKeys[0]);

        var block = blockchain.ProposeBlock(TestUtils.PrivateKeys[1]);
        // var stateChangedToRoundOnePreVote = new AsyncAutoResetEvent();
        // using var _ = consensus.StateChanged.Subscribe(state =>
        // {
        //     if (state.Round == 1 && state.Step == ConsensusStep.PreVote)
        //     {
        //         stateChangedToRoundOnePreVote.Set();
        //     }
        // });

        await consensus.StartAsync(default);
        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[2],
                    TestUtils.Validators[2].Power,
                    1,
                    1,
                    hash: block.BlockHash,
                    flag: VoteType.PreVote)
            });
        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[3],
                    TestUtils.Validators[3].Power,
                    1,
                    1,
                    hash: block.BlockHash,
                    flag: VoteType.PreVote)
            });

        // Wait for round 1 prevote step.
        // await stateChangedToRoundOnePreVote.WaitAsync();
        await consensus.WaitUntilAsync(round: 1, step: ConsensusStep.PreVote, default);

        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(1, consensus.Round);
    }

    [Fact(Timeout = Timeout)]
    public async Task EnterPreCommitBlockTwoThird()
    {
        var stepChangedToPreCommit = new AsyncAutoResetEvent();
        ConsensusPreCommitMessage? preCommit = null;
        var preCommitSent = new AsyncAutoResetEvent();
        var blockchain = TestUtils.CreateBlockchain();
        await using var consensus = TestUtils.CreateConsensus(
            blockchain: blockchain,
            privateKey: TestUtils.PrivateKeys[0]);

        var block = blockchain.ProposeBlock(TestUtils.PrivateKeys[1]);

        // using var _1 = consensus.StateChanged.Subscribe(state =>
        // {
        //     if (state.Step == ConsensusStep.PreCommit)
        //     {
        //         stepChangedToPreCommit.Set();
        //     }
        // });
        // using var _2 = consensus.MessagePublished.Subscribe(message =>
        // {
        //     if (message is ConsensusPreCommitMessage preCommitMsg)
        //     {
        //         preCommit = preCommitMsg;
        //         preCommitSent.Set();
        //     }
        // });

        await consensus.StartAsync(default);
        consensus.ProduceMessage(
            TestUtils.CreateConsensusPropose(block, TestUtils.PrivateKeys[1]));

        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[1],
                    TestUtils.Validators[1].Power,
                    1,
                    0,
                    hash: block.BlockHash,
                    VoteType.PreVote)
            });
        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[2],
                    TestUtils.Validators[2].Power,
                    1,
                    0,
                    hash: block.BlockHash,
                    VoteType.PreVote)
            });
        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[3],
                    TestUtils.Validators[3].Power,
                    1,
                    0,
                    hash: block.BlockHash,
                    VoteType.PreVote)
            });

        await Task.WhenAll(preCommitSent.WaitAsync(), stepChangedToPreCommit.WaitAsync());
        Assert.Equal(block.BlockHash, preCommit?.BlockHash);
        Assert.Equal(ConsensusStep.PreCommit, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round);

        var json =
            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(consensus.ToString())
                ?? throw new NullReferenceException("Failed to deserialize consensus");

        Assert.Equal(0, json["locked_round"].GetInt64());
        Assert.Equal(0, json["valid_round"].GetInt64());
        Assert.Equal(block.BlockHash.ToString(), json["locked_value"].GetString());
        Assert.Equal(block.BlockHash.ToString(), json["valid_value"].GetString());
    }

    [Fact(Timeout = Timeout)]
    public async Task EnterPreCommitNilTwoThird()
    {
        var stepChangedToPreCommit = new AsyncAutoResetEvent();
        var preCommitSent = new AsyncAutoResetEvent();

        var blockchain = TestUtils.CreateBlockchain();
        await using var consensus = TestUtils.CreateConsensus(
            blockchain: blockchain,
            privateKey: TestUtils.PrivateKeys[0]);

        var key = new PrivateKey();
        var invalidBlock = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = blockchain.Tip.Height + 1,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = key.Address,
                PreviousHash = blockchain.Tip.BlockHash,
            },
        }.Sign(key);

        // using var _1 = consensus.StateChanged.Subscribe(state =>
        // {
        //     if (state.Step == ConsensusStep.PreCommit)
        //     {
        //         stepChangedToPreCommit.Set();
        //     }
        // });
        // using var _2 = consensus.MessagePublished.Subscribe(message =>
        // {
        //     if (message is ConsensusPreCommitMessage preCommitMsg &&
        //         preCommitMsg.BlockHash.Equals(default))
        //     {
        //         preCommitSent.Set();
        //     }
        // });

        await consensus.StartAsync(default);
        consensus.ProduceMessage(
            TestUtils.CreateConsensusPropose(invalidBlock, TestUtils.PrivateKeys[1]));
        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[1],
                    TestUtils.Validators[1].Power,
                    1,
                    0,
                    hash: default,
                    VoteType.PreVote)
            });
        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[2],
                    TestUtils.Validators[2].Power,
                    1,
                    0,
                    hash: default,
                    VoteType.PreVote)
            });
        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[3],
                    TestUtils.Validators[3].Power,
                    1,
                    0,
                    hash: default,
                    VoteType.PreVote)
            });

        await Task.WhenAll(preCommitSent.WaitAsync(), stepChangedToPreCommit.WaitAsync());
        Assert.Equal(ConsensusStep.PreCommit, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round);
    }

    [Fact(Timeout = Timeout)]
    public async Task EnterPreVoteNilOnInvalidBlockHeader()
    {
        var stepChangedToPreVote = new AsyncAutoResetEvent();
        var timeoutProcessed = false;
        var nilPreVoteSent = new AsyncAutoResetEvent();

        var blockchain = TestUtils.CreateBlockchain();
        await using var consensus = TestUtils.CreateConsensus(
            blockchain: blockchain,
            privateKey: TestUtils.PrivateKeys[0]);
        // using var _1 = consensus.StateChanged.Subscribe(state =>
        // {
        //     if (state.Step == ConsensusStep.PreVote)
        //     {
        //         stepChangedToPreVote.Set();
        //     }
        // });
        // consensus.TimeoutProcessed += (_, __) =>
        // {
        //     timeoutProcessed = true;
        // };
        // using var _2 = consensus.MessagePublished.Subscribe(message =>
        // {
        //     if (message is ConsensusPreVoteMessage vote && vote.PreVote.BlockHash.Equals(default))
        //     {
        //         nilPreVoteSent.Set();
        //     }
        // });

        // 1. ProtocolVersion should be matched.
        // 2. Index should be increased monotonically.
        // 3. Timestamp should be increased monotonically.
        // 4. PreviousHash should be matched with Tip hash.
        var invalidBlock = new RawBlock
        {
            Header = new BlockHeader
            {
                BlockVersion = BlockHeader.CurrentProtocolVersion,
                Height = blockchain.Tip.Height + 2,
                Timestamp = blockchain.Tip.Timestamp.Subtract(TimeSpan.FromSeconds(1)),
                Proposer = TestUtils.PrivateKeys[1].Address,
                PreviousHash = blockchain.Tip.BlockHash,
            },
        }.Sign(TestUtils.PrivateKeys[1]);

        await consensus.StartAsync(default);
        consensus.ProduceMessage(
            TestUtils.CreateConsensusPropose(
                invalidBlock, TestUtils.PrivateKeys[1]));

        await Task.WhenAll(nilPreVoteSent.WaitAsync(), stepChangedToPreVote.WaitAsync());
        Assert.False(timeoutProcessed); // Check step transition isn't by timeout.
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round);
    }

    [Fact(Timeout = Timeout)]
    public async Task EnterPreVoteNilOnInvalidBlockContent()
    {
        // NOTE: This test does not check tx nonces, different state root hash.
        var stepChangedToPreVote = new AsyncAutoResetEvent();
        var timeoutProcessed = false;
        var nilPreVoteSent = new AsyncAutoResetEvent();
        var invalidKey = new PrivateKey();
        var policy = new BlockchainOptions
        {
            SystemActions = new SystemActions
            {
                EndBlockActions = [new MinerReward(1)],
            },
            BlockOptions = new BlockOptions
            {
                MaxTransactionsBytes = 50 * 1024,
            },
            TransactionOptions = new TransactionOptions
            {
                Validator = new RelayValidator<Transaction>(IsSignerValid),
            },
        };

        static void IsSignerValid(Transaction tx)
        {
            var validAddress = TestUtils.PrivateKeys[1].Address;
            if (!tx.Signer.Equals(validAddress))
            {
                throw new InvalidOperationException("invalid signer");
            }
        }

        var blockchain = TestUtils.CreateBlockchain(policy);
        await using var consensus = TestUtils.CreateConsensus(
            blockchain: blockchain,
            privateKey: TestUtils.PrivateKeys[0]);
        // using var _1 = consensus.StateChanged.Subscribe(state =>
        // {
        //     if (state.Step == ConsensusStep.PreVote)
        //     {
        //         stepChangedToPreVote.Set();
        //     }
        // });
        // consensus.TimeoutProcessed += (_, __) =>
        // {
        //     timeoutProcessed = true;
        // };
        // using var _2 = consensus.MessagePublished.Subscribe(message =>
        // {
        //     if (message is ConsensusPreVoteMessage vote && vote.PreVote.BlockHash.Equals(default))
        //     {
        //         nilPreVoteSent.Set();
        //     }
        // });

        var diffPolicyBlockChain = TestUtils.CreateBlockchain(policy, blockchain.Genesis);

        var invalidTx = diffPolicyBlockChain.StagedTransactions.Add(invalidKey);

        Block invalidBlock = Libplanet.Tests.TestUtils.ProposeNext(
            blockchain.Genesis,
            previousStateRootHash: default,
            transactions: [invalidTx],
            proposer: TestUtils.PrivateKeys[1],
            blockInterval: TimeSpan.FromSeconds(10)).Sign(TestUtils.PrivateKeys[1]);

        await consensus.StartAsync(default);
        consensus.ProduceMessage(
            TestUtils.CreateConsensusPropose(
                invalidBlock,
                TestUtils.PrivateKeys[1]));

        await Task.WhenAll(nilPreVoteSent.WaitAsync(), stepChangedToPreVote.WaitAsync());
        Assert.False(timeoutProcessed); // Check step transition isn't by timeout.
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round);
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
        var policy = new BlockchainOptions
        {
            SystemActions = new SystemActions
            {
                EndBlockActions = [new MinerReward(1)],
            },
            BlockOptions = new BlockOptions
            {
                MaxTransactionsBytes = 50 * 1024,
            },
        };

        var blockchain = TestUtils.CreateBlockchain(policy);
        await using var consensus = TestUtils.CreateConsensus(
            blockchain: blockchain,
            privateKey: TestUtils.PrivateKeys[0]);
        // using var _1 = consensus.StateChanged.Subscribe(state =>
        // {
        //     if (state.Step == ConsensusStep.PreVote)
        //     {
        //         stepChangedToPreVote.Set();
        //     }
        // });
        // consensus.TimeoutProcessed += (_, __) =>
        // {
        //     timeoutProcessed = true;
        // };
        // using var _2 = consensus.MessagePublished.Subscribe(message =>
        // {
        //     if (message is ConsensusPreVoteMessage vote && vote.PreVote.BlockHash.Equals(default))
        //     {
        //         nilPreVoteSent.Set();
        //     }
        //     else if (
        //         message is ConsensusPreCommitMessage commit &&
        //         commit.PreCommit.BlockHash.Equals(default))
        //     {
        //         nilPreCommitSent.Set();
        //     }
        // });

        using var fx = new MemoryRepositoryFixture();

        // var unsignedInvalidTx = new UnsignedTx
        // {
        //     Invoice = new TxInvoice
        //     {
        //         GenesisHash = blockchain.Genesis.BlockHash,
        //         Timestamp = DateTimeOffset.UtcNow,
        //         Actions = [new ActionBytecode([0x01])], // Invalid action
        //     },
        //     SigningMetadata = new TxSigningMetadata
        //     {
        //         Signer = txSigner.Address,
        //     },
        // };
        var invalidTx = new TransactionMetadata
        {
            GenesisHash = blockchain.Genesis.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Actions = [new ActionBytecode([0x01])], // Invalid action
            Signer = txSigner.Address,
        }.Sign(txSigner);
        var txs = new[] { invalidTx };
        var evs = Array.Empty<EvidenceBase>();

        var header = new BlockHeader
        {
            Height = 1,
            Timestamp = DateTimeOffset.UtcNow,
            Proposer = TestUtils.PrivateKeys[1].Address,
            PreviousHash = blockchain.Genesis.BlockHash,
            PreviousStateRootHash = HashDigest<SHA256>.HashData(RandomUtility.Bytes(1024)),
        };
        var preEval = new RawBlock
        {
            Header = header,
            Content = new BlockContent
            {
                Transactions = [.. txs],
                Evidences = [.. evs],
            },
        };
        var invalidBlock = preEval.Sign(TestUtils.PrivateKeys[1]);

        await consensus.StartAsync(default);
        consensus.ProduceMessage(
            TestUtils.CreateConsensusPropose(
                invalidBlock,
                TestUtils.PrivateKeys[1]));
        await Task.WhenAll(nilPreVoteSent.WaitAsync(), stepChangedToPreVote.WaitAsync());
        Assert.False(timeoutProcessed); // Check step transition isn't by timeout.
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round);

        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[1],
                    TestUtils.Validators[1].Power,
                    1,
                    0,
                    invalidBlock.BlockHash,
                    VoteType.PreVote)
            });
        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[2],
                    TestUtils.Validators[2].Power,
                    1,
                    0,
                    default,
                    VoteType.PreVote)
            });
        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[3],
                    TestUtils.Validators[3].Power,
                    1,
                    0,
                    default,
                    VoteType.PreVote)
            });
        await nilPreCommitSent.WaitAsync();
        Assert.Equal(ConsensusStep.PreCommit, consensus.Step);
    }

    [Fact(Timeout = Timeout)]
    public async Task EnterPreVoteNilOneThird()
    {
        var blockchain = TestUtils.CreateBlockchain();
        await using var consensus = TestUtils.CreateConsensus(
            blockchain: blockchain,
            privateKey: TestUtils.PrivateKeys[0]);

        var block = blockchain.ProposeBlock(TestUtils.PrivateKeys[1]);
        var stepChangedToRoundOnePreVote = new AsyncAutoResetEvent();
        // using var _ = consensus.StateChanged.Subscribe(state =>
        // {
        //     if (state.Round == 1 && state.Step == ConsensusStep.PreVote)
        //     {
        //         stepChangedToRoundOnePreVote.Set();
        //     }
        // });
        await consensus.StartAsync(default);

        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[2],
                    TestUtils.Validators[2].Power,
                    1,
                    1,
                    hash: default,
                    flag: VoteType.PreVote)
            });
        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[3],
                    TestUtils.Validators[3].Power,
                    1,
                    1,
                    hash: default,
                    flag: VoteType.PreVote)
            });

        await stepChangedToRoundOnePreVote.WaitAsync();
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(1, consensus.Round);
    }

    [Fact(Timeout = Timeout)]
    public async Task TimeoutPropose()
    {
        var stepChangedToPreVote = new AsyncAutoResetEvent();
        var preVoteSent = new AsyncAutoResetEvent();

        await using var consensus = TestUtils.CreateConsensus(
            privateKey: TestUtils.PrivateKeys[0],
            options: new ConsensusOptions
            {
                ProposeTimeoutBase = 1_000
            });

        // using var _1 = consensus.StateChanged.Subscribe(state =>
        // {
        //     if (state.Step == ConsensusStep.PreVote)
        //     {
        //         stepChangedToPreVote.Set();
        //     }
        // });
        // using var _2 = consensus.MessagePublished.Subscribe(message =>
        // {
        //     if (message is ConsensusPreVoteMessage)
        //     {
        //         preVoteSent.Set();
        //     }
        // });

        await consensus.StartAsync(default);
        await Task.WhenAll(preVoteSent.WaitAsync(), stepChangedToPreVote.WaitAsync());
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round);
    }

    [Fact(Timeout = Timeout)]
    public async Task UponRulesCheckAfterTimeout()
    {
        var blockchain = TestUtils.CreateBlockchain();
        await using var consensus = TestUtils.CreateConsensus(
            blockchain: blockchain,
            privateKey: TestUtils.PrivateKeys[0],
            options: new ConsensusOptions
            {
                PreVoteTimeoutBase = 1_000,
                PreCommitTimeoutBase = 1_000,
            });

        var block1 = blockchain.ProposeBlock(TestUtils.PrivateKeys[1]);
        var block2 = blockchain.ProposeBlock(TestUtils.PrivateKeys[2]);
        var roundOneStepChangedToPreVote = new AsyncAutoResetEvent();
        // using var _ =consensus.StateChanged.Subscribe(state =>
        // {
        //     if (state.Round == 1 && state.Step == ConsensusStep.PreVote)
        //     {
        //         roundOneStepChangedToPreVote.Set();
        //     }
        // });

        // Push round 0 and round 1 proposes.
        consensus.ProduceMessage(
            TestUtils.CreateConsensusPropose(
                block1, TestUtils.PrivateKeys[1], round: 0));
        consensus.ProduceMessage(
            TestUtils.CreateConsensusPropose(
                block2, TestUtils.PrivateKeys[2], round: 1));

        // Two additional votes should be enough to trigger prevote timeout timer.
        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[2],
                    TestUtils.Validators[2].Power,
                    1,
                    0,
                    hash: default,
                    flag: VoteType.PreVote)
            });
        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[3],
                    TestUtils.Validators[3].Power,
                    1,
                    0,
                    hash: default,
                    flag: VoteType.PreVote)
            });

        // Two additional votes should be enough to trigger precommit timeout timer.
        consensus.ProduceMessage(
            new ConsensusPreCommitMessage
            {
                PreCommit = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[2],
                    TestUtils.Validators[2].Power,
                    1,
                    0,
                    hash: default,
                    flag: VoteType.PreCommit)
            });
        consensus.ProduceMessage(
            new ConsensusPreCommitMessage
            {
                PreCommit = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[3],
                    TestUtils.Validators[3].Power,
                    1,
                    0,
                    hash: default,
                    flag: VoteType.PreCommit)
            });

        await consensus.StartAsync(default);

        // Round 0 Propose -> Round 0 PreVote (due to Round 0 Propose message) ->
        // PreVote timeout start (due to PreVote messages) ->
        // PreVote timeout end -> Round 0 PreCommit ->
        // PreCommit timeout start (due to state mutation check and PreCommit messages) ->
        // PreCommit timeout end -> Round 1 Propose ->
        // Round 1 PreVote (due to state mutation check and Round 1 Propose message)
        await roundOneStepChangedToPreVote.WaitAsync();
        Assert.Equal(1, consensus.Height);
        Assert.Equal(1, consensus.Round);
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
    }

    [Fact(Timeout = Timeout)]
    public async Task TimeoutPreVote()
    {
        var blockchain = TestUtils.CreateBlockchain();
        await using var consensus = TestUtils.CreateConsensus(
            blockchain: blockchain,
            privateKey: TestUtils.PrivateKeys[0],
            options: new ConsensusOptions
            {
                PreVoteTimeoutBase = 1_000,
            });

        var block = blockchain.ProposeBlock(TestUtils.PrivateKeys[1]);
        var timeoutProcessed = new AsyncAutoResetEvent();
        // consensus.TimeoutProcessed += (_, __) => timeoutProcessed.Set();
        await consensus.StartAsync(default);

        consensus.ProduceMessage(
            TestUtils.CreateConsensusPropose(
                block, TestUtils.PrivateKeys[1], round: 0));

        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[1],
                    TestUtils.Validators[1].Power,
                    1,
                    0,
                    hash: block.BlockHash,
                    flag: VoteType.PreVote)
            });
        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[2],
                    TestUtils.Validators[2].Power,
                    1,
                    0,
                    hash: default,
                    flag: VoteType.PreVote)
            });
        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[3],
                    TestUtils.Validators[3].Power,
                    1,
                    0,
                    hash: default,
                    flag: VoteType.PreVote)
            });

        // Wait for timeout.
        await timeoutProcessed.WaitAsync();
        Assert.Equal(ConsensusStep.PreCommit, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round);
    }

    [Fact(Timeout = Timeout)]
    public async Task TimeoutPreCommit()
    {
        var blockchain = TestUtils.CreateBlockchain();
        await using var consensus = TestUtils.CreateConsensus(
            blockchain: blockchain,
            privateKey: TestUtils.PrivateKeys[0],
            options: new ConsensusOptions
            {
                PreCommitTimeoutBase = 1_000,
            });

        var block = blockchain.ProposeBlock(TestUtils.PrivateKeys[1]);
        var timeoutProcessed = new AsyncAutoResetEvent();
        // consensus.TimeoutProcessed += (_, __) => timeoutProcessed.Set();
        await consensus.StartAsync(default);

        consensus.ProduceMessage(
            TestUtils.CreateConsensusPropose(
                block, TestUtils.PrivateKeys[1], round: 0));

        consensus.ProduceMessage(
            new ConsensusPreCommitMessage
            {
                PreCommit = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[1],
                    TestUtils.Validators[1].Power,
                    1,
                    0,
                    hash: block.BlockHash,
                    flag: VoteType.PreCommit)
            });
        consensus.ProduceMessage(
            new ConsensusPreCommitMessage
            {
                PreCommit = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[2],
                    TestUtils.Validators[2].Power,
                    1,
                    0,
                    hash: default,
                    flag: VoteType.PreCommit)
            });
        consensus.ProduceMessage(
            new ConsensusPreCommitMessage
            {
                PreCommit = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[3],
                    TestUtils.Validators[3].Power,
                    1,
                    0,
                    hash: default,
                    flag: VoteType.PreCommit)
            });

        // Wait for timeout.
        await timeoutProcessed.WaitAsync();
        Assert.Equal(ConsensusStep.Propose, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(1, consensus.Round);
    }
}
