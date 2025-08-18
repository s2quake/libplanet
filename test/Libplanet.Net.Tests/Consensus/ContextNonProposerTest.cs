using System.Security.Cryptography;
using System.Text.Json;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Tests.Store;
using Libplanet.Types;
using Nito.AsyncEx;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Extensions;
using static Libplanet.Net.Tests.TestUtils;
using Libplanet.Extensions;
using System.Reactive.Linq;

namespace Libplanet.Net.Tests.Consensus;

public class ContextNonProposerTest
{
    [Fact(Timeout = TestUtils.Timeout)]
    public async Task EnterPreVoteBlockOneThird()
    {
        var blockchain = MakeBlockchain();
        var options = new ConsensusOptions
        {
            ProposeTimeoutBase = TimeSpan.FromSeconds(100),
        };
        await using var consensus = new Net.Consensus.Consensus(Validators, 1, options);
        var block = blockchain.ProposeBlock(Signers[1]);
        var preVoteStepRound1Task = consensus.StepChanged.WaitAsync(
            s => s.Step == ConsensusStep.Propose && consensus.Round.Index == 1);

        await consensus.StartAsync();
        _ = consensus.PreVoteAsync(
            new VoteBuilder
            {
                Validator = Validators[2],
                Block = block,
                Round = 1,
                Type = VoteType.PreVote,
            }.Create(Signers[2]));
        _ = consensus.PreVoteAsync(
            new VoteBuilder
            {
                Validator = Validators[3],
                Block = block,
                Round = 1,
                Type = VoteType.PreVote,
            }.Create(Signers[3]));

        // Wait for round 1 prevote step.
        await preVoteStepRound1Task.WaitAsync(WaitTimeout);

        Assert.Equal(ConsensusStep.Propose, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(1, consensus.Round.Index);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task EnterPreCommitBlockTwoThird()
    {
        var blockchain = MakeBlockchain();
        await using var consensus = new Net.Consensus.Consensus(Validators);
        var preCommitStepTask = consensus.StepChanged.WaitAsync(s => s.Step == ConsensusStep.PreCommit);
        var preVoteMaj23Task = consensus.PreVoteMaj23Observed.WaitAsync();
        var block = blockchain.ProposeBlock(Signers[1]);

        await consensus.StartAsync();

        _ = consensus.ProposeAsync(
            new ProposalBuilder
            {
                Block = block,
            }.Create(Signers[1]));
        _ = consensus.PreVoteAsync(
            new VoteBuilder
            {
                Validator = Validators[1],
                Block = block,
                Type = VoteType.PreVote,
            }.Create(Signers[1]));
        _ = consensus.PreVoteAsync(
            new VoteBuilder
            {
                Validator = Validators[2],
                Block = block,
                Type = VoteType.PreVote,
            }.Create(Signers[2]));
        _ = consensus.PreVoteAsync(
            new VoteBuilder
            {
                Validator = Validators[3],
                Block = block,
                Type = VoteType.PreVote,
            }.Create(Signers[3]));

        var (_, preCommitBlockHash) = await preCommitStepTask.WaitAsync(WaitTimeout);
        Assert.Equal(block.BlockHash, preCommitBlockHash);
        Assert.Equal(ConsensusStep.PreCommit, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round.Index);

        var preVoteMaj23 = await preVoteMaj23Task.WaitAsync(WaitTimeout);
        Assert.NotNull(consensus.ValidProposal);
        Assert.Equal(block.BlockHash, consensus.ValidProposal.BlockHash);
        Assert.Equal(block.BlockHash, preVoteMaj23.BlockHash);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task EnterPreCommitNilTwoThird()
    {
        var blockchain = MakeBlockchain();
        await using var consensus = new Net.Consensus.Consensus(Validators);
        var preCommitStepTask = consensus.StepChanged.WaitAsync(s => s.Step == ConsensusStep.PreCommit);
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

        await consensus.StartAsync();

        _ = consensus.ProposeAsync(
            new ProposalBuilder
            {
                Block = invalidBlock,
            }.Create(Signers[1]));
        _ = consensus.PreVoteAsync(
            new NilVoteBuilder
            {
                Validator = Validators[1],
                Height = 1,
                Type = VoteType.PreVote,
            }.Create(Signers[1]));
        _ = consensus.PreVoteAsync(
            new NilVoteBuilder
            {
                Validator = Validators[2],
                Height = 1,
                Type = VoteType.PreVote,
            }.Create(Signers[2]));
        _ = consensus.PreVoteAsync(
            new NilVoteBuilder
            {
                Validator = Validators[3],
                Height = 1,
                Type = VoteType.PreVote,
            }.Create(Signers[3]));

        var (_, preCommitBlockHash) = await preCommitStepTask.WaitAsync(WaitTimeout);
        Assert.Equal(default, preCommitBlockHash);
        Assert.Equal(ConsensusStep.PreCommit, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round.Index);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task EnterPreVoteNilOnInvalidBlockHeader()
    {
        var blockchain = MakeBlockchain();
        await using var consensus = new Net.Consensus.Consensus(Validators);
        var preVoteStepTask = consensus.StepChanged.WaitAsync(
            e => e.Step == ConsensusStep.PreVote && e.BlockHash == default && consensus.Round.Index == 0);
        var proposeTimeoutTask = consensus.TimeoutOccurred.WaitAsync(e => e == ConsensusStep.Propose);

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
                Proposer = PrivateKeys[1].Address,
                PreviousHash = blockchain.Tip.BlockHash,
            },
        }.Sign(PrivateKeys[1]);

        await consensus.StartAsync();

        _ = consensus.ProposeAsync(
            new ProposalBuilder
            {
                Block = invalidBlock,
            }.Create(Signers[1]));

        await proposeTimeoutTask.WaitAsync(WaitTimeout);
        await preVoteStepTask.WaitAsync(WaitTimeout);

        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round.Index);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task EnterPreVoteNilOnInvalidBlockContent()
    {
        // NOTE: This test does not check tx nonces, different state root hash.
        var invalidKey = new PrivateKey();
        var blockchainOptions = new BlockchainOptions
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
                Validators =
                [
                    new RelayObjectValidator<Transaction>(IsSignerValid),
                ]
            },
        };

        static void IsSignerValid(Transaction tx)
        {
            var validAddress = PrivateKeys[1].Address;
            if (!tx.Signer.Equals(validAddress))
            {
                throw new InvalidOperationException("invalid signer");
            }
        }

        var blockchain = MakeBlockchain(blockchainOptions);
        var options = new ConsensusOptions
        {
            BlockValidators =
            [
                new RelayObjectValidator<Block>(blockchain.Validate),
            ],
        };
        await using var consensus = new Net.Consensus.Consensus(Validators, options);
        var preVoteStepTask = consensus.StepChanged.WaitAsync(
            e => e.Step == ConsensusStep.PreVote && e.BlockHash == default && consensus.Round.Index == 0);
        var proposeTimeoutTask = consensus.TimeoutOccurred.WaitAsync(e => e == ConsensusStep.Propose);

        var invalidTx = new TransactionBuilder
        {
        }.Create(invalidKey.AsSigner());
        var invalidBlock = new BlockBuilder
        {
            Transactions = [invalidTx],
            Timestamp = blockchain.Tip.Timestamp.AddSeconds(15),
        }.Create(Signers[1], blockchain);

        await consensus.StartAsync();

        _ = consensus.ProposeAsync(
            new ProposalBuilder
            {
                Block = invalidBlock,
            }.Create(Signers[1]));

        await preVoteStepTask.WaitAsync(WaitTimeout);
        await Assert.ThrowsAsync<TimeoutException>(() => proposeTimeoutTask.WaitAsync(WaitTimeout));
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round.Index);
    }

    [Fact(Timeout = TestUtils.Timeout)]
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

        var blockchain = MakeBlockchain(policy);
        await using var consensus = new Net.Consensus.Consensus(Validators);
        // privateKey: TestUtils.PrivateKeys[0]);
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
            Proposer = PrivateKeys[1].Address,
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
        var invalidBlock = preEval.Sign(PrivateKeys[1]);

        await consensus.StartAsync();
        consensus.ProduceMessage(
            CreateConsensusPropose(
                invalidBlock,
                PrivateKeys[1]));
        await Task.WhenAll(nilPreVoteSent.WaitAsync(), stepChangedToPreVote.WaitAsync());
        Assert.False(timeoutProcessed); // Check step transition isn't by timeout.
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round.Index);

        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = new VoteBuilder
                {
                    Validator = Validators[1],
                    Block = invalidBlock,
                    Type = VoteType.PreVote,
                }.Create(PrivateKeys[1])
            });
        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = new VoteMetadata
                {
                    Validator = Validators[2].Address,
                    ValidatorPower = Validators[2].Power,
                    Height = 1,
                    Type = VoteType.PreVote,
                }.Sign(PrivateKeys[2])
            });
        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = new VoteMetadata
                {
                    Validator = Validators[3].Address,
                    ValidatorPower = Validators[3].Power,
                    Height = 1,
                    Type = VoteType.PreVote,
                }.Sign(PrivateKeys[3])
            });
        await nilPreCommitSent.WaitAsync();
        Assert.Equal(ConsensusStep.PreCommit, consensus.Step);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task EnterPreVoteNilOneThird()
    {
        var blockchain = MakeBlockchain();
        await using var consensus = new Net.Consensus.Consensus(Validators);
        // privateKey: TestUtils.PrivateKeys[0]);

        var block = blockchain.ProposeBlock(PrivateKeys[1]);
        var stepChangedToRoundOnePreVote = new AsyncAutoResetEvent();
        // using var _ = consensus.StateChanged.Subscribe(state =>
        // {
        //     if (state.Round == 1 && state.Step == ConsensusStep.PreVote)
        //     {
        //         stepChangedToRoundOnePreVote.Set();
        //     }
        // });
        await consensus.StartAsync();

        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = new VoteMetadata
                {
                    Validator = Validators[2].Address,
                    ValidatorPower = Validators[2].Power,
                    Height = 1,
                    Round = 1,
                    Type = VoteType.PreVote,
                }.Sign(PrivateKeys[2])
            });
        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = new VoteMetadata
                {
                    Validator = Validators[3].Address,
                    ValidatorPower = Validators[3].Power,
                    Height = 1,
                    Round = 1,
                    Type = VoteType.PreVote,
                }.Sign(PrivateKeys[3])
            });

        await stepChangedToRoundOnePreVote.WaitAsync();
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(1, consensus.Round.Index);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task TimeoutPropose()
    {
        var stepChangedToPreVote = new AsyncAutoResetEvent();
        var preVoteSent = new AsyncAutoResetEvent();

        await using var consensus = CreateConsensus(
            // privateKey: TestUtils.PrivateKeys[0],
            options: new ConsensusOptions
            {
                ProposeTimeoutBase = TimeSpan.FromSeconds(1),
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

        await consensus.StartAsync();
        await Task.WhenAll(preVoteSent.WaitAsync(), stepChangedToPreVote.WaitAsync());
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round.Index);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task UponRulesCheckAfterTimeout()
    {
        var blockchain = MakeBlockchain();
        await using var consensus = CreateConsensus(
            options: new ConsensusOptions
            {
                PreVoteTimeoutBase = 1_000,
                PreCommitTimeoutBase = 1_000,
            });

        var block1 = blockchain.ProposeBlock(PrivateKeys[1]);
        var block2 = blockchain.ProposeBlock(PrivateKeys[2]);
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
            CreateConsensusPropose(
                block1, PrivateKeys[1], round: 0));
        consensus.ProduceMessage(
            CreateConsensusPropose(
                block2, PrivateKeys[2], round: 1));

        // Two additional votes should be enough to trigger prevote timeout timer.
        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = new VoteMetadata
                {
                    Validator = Validators[2].Address,
                    ValidatorPower = Validators[2].Power,
                    Height = 1,
                    Type = VoteType.PreVote,
                }.Sign(PrivateKeys[2])
            });
        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = new VoteMetadata
                {
                    Validator = Validators[3].Address,
                    ValidatorPower = Validators[3].Power,
                    Height = 1,
                    Type = VoteType.PreVote,
                }.Sign(PrivateKeys[3])
            });

        // Two additional votes should be enough to trigger precommit timeout timer.
        consensus.ProduceMessage(
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = Validators[2].Address,
                    ValidatorPower = Validators[2].Power,
                    Height = 1,
                    Type = VoteType.PreCommit,
                }.Sign(PrivateKeys[2])
            });
        consensus.ProduceMessage(
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = Validators[3].Address,
                    ValidatorPower = Validators[3].Power,
                    Height = 1,
                    Type = VoteType.PreCommit,
                }.Sign(PrivateKeys[3])
            });

        await consensus.StartAsync();

        // Round 0 Propose -> Round 0 PreVote (due to Round 0 Propose message) ->
        // PreVote timeout start (due to PreVote messages) ->
        // PreVote timeout end -> Round 0 PreCommit ->
        // PreCommit timeout start (due to state mutation check and PreCommit messages) ->
        // PreCommit timeout end -> Round 1 Propose ->
        // Round 1 PreVote (due to state mutation check and Round 1 Propose message)
        await roundOneStepChangedToPreVote.WaitAsync();
        Assert.Equal(1, consensus.Height);
        Assert.Equal(1, consensus.Round.Index);
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task TimeoutPreVote()
    {
        var blockchain = MakeBlockchain();
        await using var consensus = CreateConsensus(
            options: new ConsensusOptions
            {
                PreVoteTimeoutBase = 1_000,
            });

        var block = blockchain.ProposeBlock(PrivateKeys[1]);
        var timeoutProcessed = new AsyncAutoResetEvent();
        // consensus.TimeoutProcessed += (_, __) => timeoutProcessed.Set();
        await consensus.StartAsync();

        consensus.ProduceMessage(
            CreateConsensusPropose(
                block, PrivateKeys[1], round: 0));

        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = new VoteBuilder
                {
                    Validator = Validators[1],
                    Block = block,
                    Type = VoteType.PreVote,
                }.Create(PrivateKeys[1])
            });
        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                PreVote = new VoteMetadata
                {
                    Validator = Validators[2].Address,
                    ValidatorPower = Validators[2].Power,
                    Height = 1,
                    Type = VoteType.PreVote,
                }.Sign(PrivateKeys[2])
            });
        consensus.ProduceMessage(
            new ConsensusPreVoteMessage
            {
                // PreVote = TestUtils.CreateVote(
                //     TestUtils.PrivateKeys[3],
                //     TestUtils.Validators[3].Power,
                //     1,
                //     0,
                //     hash: default,
                //     flag: VoteType.PreVote)
                PreVote = new VoteMetadata
                {
                    Validator = Validators[3].Address,
                    ValidatorPower = Validators[3].Power,
                    Height = 1,
                    Type = VoteType.PreVote,
                }.Sign(PrivateKeys[3])
            });

        // Wait for timeout.
        await timeoutProcessed.WaitAsync();
        Assert.Equal(ConsensusStep.PreCommit, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round.Index);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task TimeoutPreCommit()
    {
        var blockchain = MakeBlockchain();
        await using var consensus = CreateConsensus(
            options: new ConsensusOptions
            {
                PreCommitTimeoutBase = 1_000,
            });

        var block = blockchain.ProposeBlock(PrivateKeys[1]);
        var timeoutProcessed = new AsyncAutoResetEvent();
        // consensus.TimeoutProcessed += (_, __) => timeoutProcessed.Set();
        await consensus.StartAsync();

        consensus.ProduceMessage(
            CreateConsensusPropose(
                block, PrivateKeys[1], round: 0));

        consensus.ProduceMessage(
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteBuilder
                {
                    Validator = Validators[1],
                    Block = block,
                    Type = VoteType.PreCommit,
                }.Create(PrivateKeys[1])
            });
        consensus.ProduceMessage(
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = Validators[2].Address,
                    ValidatorPower = Validators[2].Power,
                    Height = 1,
                    Type = VoteType.PreCommit,
                }.Sign(PrivateKeys[2])
            });
        consensus.ProduceMessage(
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = Validators[3].Address,
                    ValidatorPower = Validators[3].Power,
                    Height = 1,
                    Type = VoteType.PreCommit,
                }.Sign(PrivateKeys[3])
            });

        // Wait for timeout.
        await timeoutProcessed.WaitAsync();
        Assert.Equal(ConsensusStep.Propose, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(1, consensus.Round.Index);
    }
}
