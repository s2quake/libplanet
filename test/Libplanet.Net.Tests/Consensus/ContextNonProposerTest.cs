using System.Security.Cryptography;
using Libplanet.Extensions;
using Libplanet.Net.Consensus;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.TestUtilities;
using Libplanet.Types;
using static Libplanet.Net.Tests.TestUtils;

namespace Libplanet.Net.Tests.Consensus;

public class ContextNonProposerTest(ITestOutputHelper output)
{
    [Fact(Timeout = TestUtils.Timeout)]
    public async Task EnterPreVoteBlockOneThird()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var blockchain = MakeBlockchain();
        var options = new ConsensusOptions
        {
            ProposeTimeoutBase = TimeSpan.FromSeconds(100),
        };
        await using var consensus = new Net.Consensus.Consensus(Validators, 1, options);
        var block = blockchain.Propose(Signers[1]);
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
        await preVoteStepRound1Task.WaitAsync(WaitTimeout5, cancellationToken);

        Assert.Equal(ConsensusStep.Propose, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(1, consensus.Round.Index);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task EnterPreCommitBlockTwoThird()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var blockchain = MakeBlockchain();
        await using var consensus = new Net.Consensus.Consensus(Validators);
        var preCommitStepTask = consensus.StepChanged.WaitAsync(s => s.Step == ConsensusStep.PreCommit);
        var preVoteMaj23Task = consensus.PreVoteMaj23Observed.WaitAsync();
        var block = blockchain.Propose(Signers[1]);

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

        var (_, preCommitBlockHash) = await preCommitStepTask.WaitAsync(WaitTimeout5, cancellationToken);
        Assert.Equal(block.BlockHash, preCommitBlockHash);
        Assert.Equal(ConsensusStep.PreCommit, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round.Index);

        var preVoteMaj23 = await preVoteMaj23Task.WaitAsync(WaitTimeout5, cancellationToken);
        Assert.NotNull(consensus.ValidProposal);
        Assert.Equal(block.BlockHash, consensus.ValidProposal.BlockHash);
        Assert.Equal(block.BlockHash, preVoteMaj23.BlockHash);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task EnterPreCommitNilTwoThird()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var blockchain = MakeBlockchain();
        await using var consensus = new Net.Consensus.Consensus(Validators);
        var preCommitStepTask = consensus.StepChanged.WaitAsync(s => s.Step == ConsensusStep.PreCommit);
        var signer = RandomUtility.Signer(random);
        var invalidBlock = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = blockchain.Tip.Height + 1,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = signer.Address,
                PreviousBlockHash = blockchain.Tip.BlockHash,
            },
        }.Sign(signer);

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

        var (_, preCommitBlockHash) = await preCommitStepTask.WaitAsync(WaitTimeout5, cancellationToken);
        Assert.Equal(default, preCommitBlockHash);
        Assert.Equal(ConsensusStep.PreCommit, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round.Index);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task EnterPreVoteNilOnInvalidBlockHeader()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
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
                Proposer = Signers[1].Address,
                PreviousBlockHash = blockchain.Tip.BlockHash,
            },
        }.Sign(Signers[1]);

        await consensus.StartAsync();

        _ = consensus.ProposeAsync(
            new ProposalBuilder
            {
                Block = invalidBlock,
            }.Create(Signers[1]));

        await proposeTimeoutTask.WaitAsync(cancellationToken);
        await preVoteStepTask.WaitAsync(cancellationToken);

        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round.Index);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task EnterPreVoteNilOnInvalidBlockContent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
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
                MaxActionBytes = 50 * 1024,
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
            var validAddress = Signers[1].Address;
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

        await preVoteStepTask.WaitAsync(WaitTimeout5, cancellationToken);
        await Assert.ThrowsAsync<TimeoutException>(
            () => proposeTimeoutTask.WaitAsync(WaitTimeout5, cancellationToken));
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round.Index);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task EnterPreVoteNilOnInvalidAction()
    {
        // NOTE: This test does not check tx nonces, different state root hash.
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var txSigner = RandomUtility.Signer(random);
        var blockchainOptions = new BlockchainOptions
        {
            SystemActions = new SystemActions
            {
                EndBlockActions = [new MinerReward(1)],
            },
            BlockOptions = new BlockOptions
            {
                MaxActionBytes = 50 * 1024,
            },
        };

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
        var preCommitStepTask = consensus.StepChanged.WaitAsync(
            e => e.Step == ConsensusStep.PreCommit && e.BlockHash == default && consensus.Round.Index == 0);

        var invalidTx = new TransactionMetadata
        {
            GenesisBlockHash = blockchain.Genesis.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Actions = [new ActionBytecode([0x01])], // Invalid action
            Signer = txSigner.Address,
        }.Sign(txSigner);
        var invalidBlock = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = 1,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = Signers[1].Address,
                PreviousBlockHash = blockchain.Genesis.BlockHash,
                PreviousStateRootHash = RandomUtility.HashDigest<SHA256>(random),
            },
            Content = new BlockContent
            {
                Transactions = [invalidTx],
                Evidences = [],
            },
        }.Sign(Signers[1]);

        await consensus.StartAsync();

        _ = consensus.ProposeAsync(
            new ProposalBuilder
            {
                Block = invalidBlock,
            }.Create(Signers[1]),
            cancellationToken);

        await preVoteStepTask.WaitAsync(WaitTimeout5, cancellationToken);
        await Assert.ThrowsAsync<TimeoutException>(
            () => proposeTimeoutTask.WaitAsync(WaitTimeout5, cancellationToken));
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round.Index);

        _ = consensus.PreVoteAsync(
            new NilVoteBuilder
            {
                Validator = Validators[0],
                Height = 1,
                Type = VoteType.PreVote,
            }.Create(Signers[0]));
        _ = consensus.PreVoteAsync(
            new VoteBuilder
            {
                Validator = Validators[1],
                Block = invalidBlock,
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

        await preCommitStepTask.WaitAsync(WaitTimeout5, cancellationToken);
        Assert.Equal(ConsensusStep.PreCommit, consensus.Step);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task EnterPreVoteNilOneThird()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var blockchain = MakeBlockchain();
        await using var consensus = new Net.Consensus.Consensus(Validators);
        var preVoteStepRound1Task = consensus.StepChanged.WaitAsync(
            s => s.Step == ConsensusStep.PreVote && consensus.Round.Index == 1);

        _ = blockchain.Propose(Signers[1]);
        await consensus.StartAsync();

        _ = consensus.PreVoteAsync(
            new NilVoteBuilder
            {
                Validator = Validators[2],
                Height = 1,
                Round = 1,
                Type = VoteType.PreVote,
            }.Create(Signers[2]));
        _ = consensus.PreVoteAsync(
            new NilVoteBuilder
            {
                Validator = Validators[3],
                Height = 1,
                Round = 1,
                Type = VoteType.PreVote,
            }.Create(Signers[3]));

        await preVoteStepRound1Task.WaitAsync(cancellationToken);
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(1, consensus.Round.Index);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task TimeoutPropose()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = new ConsensusOptions
        {
            ProposeTimeoutBase = TimeSpan.FromSeconds(1),
        };
        await using var consensus = new Net.Consensus.Consensus(Validators, options);
        var preVoteStepTask = consensus.StepChanged.WaitAsync(
            s => s.Step == ConsensusStep.PreVote && consensus.Round.Index == 0);


        await consensus.StartAsync();

        await preVoteStepTask.WaitAsync(cancellationToken);
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round.Index);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task UponRulesCheckAfterTimeout()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var blockchain = MakeBlockchain();
        var options = new ConsensusOptions
        {
            PreVoteTimeoutBase = 1_000,
            PreCommitTimeoutBase = 1_000,
        };
        await using var consensus = new Net.Consensus.Consensus(Validators, options);
        var preVoteStepTask = consensus.StepChanged.WaitAsync(
            s => s.Step == ConsensusStep.PreVote && consensus.Round.Index == 1);

        var block1 = blockchain.Propose(Signers[1]);
        var block2 = blockchain.Propose(Signers[2]);

        await consensus.StartAsync(cancellationToken);

        // Push round 0 and round 1 proposes.
        _ = consensus.ProposeAsync(
            new ProposalBuilder
            {
                Block = block1,
                Round = 0,
            }.Create(Signers[1]));
        _ = consensus.ProposeAsync(
        new ProposalBuilder
        {
            Block = block2,
            Round = 1,
        }.Create(Signers[2]));

        // Two additional votes should be enough to trigger prevote timeout timer.
        _ = consensus.PreVoteAsync(
            new VoteBuilder
            {
                Validator = Validators[0],
                Block = block1,
                Type = VoteType.PreVote,
            }.Create(Signers[0]));
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

        // Two additional votes should be enough to trigger precommit timeout timer.
        _ = consensus.PreCommitAsync(
            new NilVoteBuilder
            {
                Validator = Validators[0],
                Height = 1,
                Type = VoteType.PreCommit,
            }.Create(Signers[0]));
        _ = consensus.PreCommitAsync(
            new NilVoteBuilder
            {
                Validator = Validators[2],
                Height = 1,
                Type = VoteType.PreCommit,
            }.Create(Signers[2]));
        _ = consensus.PreCommitAsync(
            new NilVoteBuilder
            {
                Validator = Validators[3],
                Height = 1,
                Type = VoteType.PreCommit,
            }.Create(Signers[3]));

        // Round 0 Propose -> Round 0 PreVote (due to Round 0 Propose message) ->
        // PreVote timeout start (due to PreVote messages) ->
        // PreVote timeout end -> Round 0 PreCommit ->
        // PreCommit timeout start (due to state mutation check and PreCommit messages) ->
        // PreCommit timeout end -> Round 1 Propose ->
        // Round 1 PreVote (due to state mutation check and Round 1 Propose message)
        await preVoteStepTask.WaitAsync(cancellationToken);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(1, consensus.Round.Index);
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task TimeoutPreVote()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var blockchain = MakeBlockchain();
        var options = new ConsensusOptions
        {
            PreVoteTimeoutBase = 1_000,
        };
        await using var consensus = new Net.Consensus.Consensus(Validators, options);
        var timeoutTask = consensus.TimeoutOccurred.WaitAsync(
            e => e == ConsensusStep.PreVote && consensus.Height == 1 && consensus.Round.Index == 0);

        var block = blockchain.Propose(Signers[1]);

        await consensus.StartAsync();

        _ = consensus.ProposeAsync(
            new ProposalBuilder
            {
                Block = block,
            }.Create(Signers[1]));

        _ = consensus.PreVoteAsync(
            new VoteBuilder
            {
                Validator = Validators[0],
                Block = block,
                Type = VoteType.PreVote,
            }.Create(Signers[0]));
        _ = consensus.PreVoteAsync(
            new VoteBuilder
            {
                Validator = Validators[1],
                Block = block,
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

        // Wait for timeout.
        await timeoutTask.WaitAsync(cancellationToken);
        Assert.Equal(ConsensusStep.PreCommit, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round.Index);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task TimeoutPreCommit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var blockchain = MakeBlockchain();
        var options = new ConsensusOptions
        {
            PreCommitTimeoutBase = 1_000,
        };
        await using var consensus = new Net.Consensus.Consensus(Validators, options);
        var timeoutTask = consensus.TimeoutOccurred.WaitAsync(
            e => e == ConsensusStep.PreCommit && consensus.Height == 1 && consensus.Round.Index == 0);
        var proposeStep1Task = consensus.StepChanged.WaitAsync(
            e => e.Step == ConsensusStep.Propose && consensus.Round.Index == 1);

        var block = blockchain.Propose(Signers[1]);

        await consensus.StartAsync();

        _ = consensus.ProposeAsync(
            new ProposalBuilder
            {
                Block = block,
            }.Create(Signers[1]));

        _ = consensus.PreCommitAsync(
            new VoteBuilder
            {
                Validator = Validators[0],
                Block = block,
                Type = VoteType.PreCommit,
            }.Create(Signers[0]));
        _ = consensus.PreCommitAsync(
            new VoteBuilder
            {
                Validator = Validators[1],
                Block = block,
                Type = VoteType.PreCommit,
            }.Create(Signers[1]));

        _ = consensus.PreCommitAsync(
            new NilVoteBuilder
            {
                Validator = Validators[2],
                Height = 1,
                Type = VoteType.PreCommit,
            }.Create(Signers[2]));

        _ = consensus.PreCommitAsync(
            new NilVoteBuilder
            {
                Validator = Validators[3],
                Height = 1,
                Type = VoteType.PreCommit,
            }.Create(Signers[3]));


        // Wait for timeout.
        await timeoutTask.WaitAsync(cancellationToken);
        await proposeStep1Task.WaitAsync(cancellationToken);
        Assert.Equal(ConsensusStep.Propose, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(1, consensus.Round.Index);
    }
}
