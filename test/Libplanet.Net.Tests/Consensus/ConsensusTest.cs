using System.Reactive.Linq;
using Libplanet.Extensions;
using Libplanet.Net.Consensus;
using Libplanet.TestUtilities;
using static Libplanet.Net.Tests.TestUtils;

namespace Libplanet.Net.Tests.Consensus;

public sealed class ConsensusTest(ITestOutputHelper output)
{
    [Fact(Timeout = TestUtils.Timeout)]
    public async Task BaseTest()
    {
        await using var consensus = new Net.Consensus.Consensus(Validators);

        Assert.Equal(1, consensus.Height);
        Assert.Throws<InvalidOperationException>(() => consensus.Round);
        Assert.Equal(ConsensusStep.Default, consensus.Step);
        Assert.Null(consensus.Proposal);
        Assert.Equal(Validators, consensus.Validators);
    }

    [Theory(Timeout = TestUtils.Timeout)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public async Task StartAsync(int index, bool isProposer)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = Rand.GetRandom(output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        await using var consensus = new Net.Consensus.Consensus(Validators);
        using var consensusController = new ConsensusObserver(
            Signers[index],
            consensus,
            blockchain);
        var proposedTask = consensusController.ShouldPropose.WaitAsync();

        await consensus.StartAsync(cancellationToken);

        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round.Index);
        Assert.Equal(ConsensusStep.Propose, consensus.Step);
        Assert.Null(consensus.Proposal);
        if (isProposer)
        {
            await proposedTask;
        }
        else
        {
            await Assert.ThrowsAsync<TimeoutException>(
                () => proposedTask.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken));
        }
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task StopAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var consensus = new Net.Consensus.Consensus(
            height: 1,
            validators: Validators,
            options: new ConsensusOptions());

        await consensus.StartAsync(cancellationToken);
        await consensus.StopAsync(cancellationToken);

        Assert.Equal(1, consensus.Height);
        Assert.Throws<InvalidOperationException>(() => consensus.Round.Index);
        Assert.Equal(ConsensusStep.Default, consensus.Step);
        Assert.Null(consensus.Proposal);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task DisposeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var consensus1 = new Net.Consensus.Consensus(
            height: 1,
            validators: Validators,
            options: new ConsensusOptions());
        var consensus2 = new Net.Consensus.Consensus(
            height: 1,
            validators: Validators,
            options: new ConsensusOptions());

        await consensus1.StartAsync(cancellationToken);
        await consensus1.DisposeAsync();
        await consensus2.DisposeAsync();

        Assert.True(consensus1.IsDisposed);
        Assert.True(consensus2.IsDisposed);

        await consensus1.DisposeAsync();
        await consensus2.DisposeAsync();
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task Propose()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = Rand.GetRandom(output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var options = new ConsensusOptions
        {
            ProposeTimeoutBase = TimeSpan.FromSeconds(1),
            ProposeTimeoutDelta = TimeSpan.FromMilliseconds(100),
        };
        await using var consensus = new Net.Consensus.Consensus(
            height: 1,
            // signer: TestUtils.PrivateKeys[0].AsSigner(),
            validators: Validators,
            options: options);

        Assert.Equal(ConsensusStep.Default, consensus.Step);

        await consensus.StartAsync(cancellationToken);
        Assert.Equal(ConsensusStep.Propose, consensus.Step);

        var proposal = new ProposalBuilder
        {
            Block = blockchain.Propose(Signers[1]),
        }.Create(Signers[1]);
        var stepChangedTask = consensus.StepChanged.WaitAsync(cancellationToken);
        _ = consensus.ProposeAsync(proposal, cancellationToken);
        await stepChangedTask.WaitAsync(options.TimeoutPropose(consensus.Round), cancellationToken);
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(proposal, consensus.Proposal);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task Propose_Throw_Double_Proposal()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = Rand.GetRandom(output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var options = new ConsensusOptions
        {
            ProposeTimeoutBase = TimeSpan.FromSeconds(2),
            ProposeTimeoutDelta = TimeSpan.FromMilliseconds(100),
        };
        await using var consensus = new Net.Consensus.Consensus(
            height: 1,
            // signer: TestUtils.PrivateKeys[0].AsSigner(),
            validators: Validators,
            options: options);

        await consensus.StartAsync(cancellationToken);

        var proposal = new ProposalBuilder
        {
            Block = blockchain.Propose(Signers[1]),
        }.Create(Signers[1]);
        var stepChangedTask = consensus.StepChanged.WaitAsync();
        _ = consensus.ProposeAsync(proposal, cancellationToken);

        var exceptionOccurredTask = consensus.ExceptionOccurred.WaitAsync(cancellationToken);
        _ = consensus.ProposeAsync(proposal, cancellationToken);
        var e1 = await exceptionOccurredTask.WaitAsync(WaitTimeout5, cancellationToken);
        var e2 = Assert.IsType<InvalidOperationException>(e1);
        Assert.StartsWith("Proposal already exists", e2.Message);

        await stepChangedTask.WaitAsync(options.TimeoutPropose(consensus.Round), cancellationToken);
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task Propose_Throw_With_InvalidProposer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = Rand.GetRandom(output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        await using var consensus = new Net.Consensus.Consensus(
            height: 1,
            // signer: TestUtils.PrivateKeys[0].AsSigner(),
            validators: Validators,
            options: new());

        await consensus.StartAsync(cancellationToken);

        var proposal = new ProposalBuilder
        {
            Block = blockchain.Propose(Signers[0]),
        }.Create(Signers[0]);
        var exceptionOccurredTask = consensus.ExceptionOccurred.WaitAsync();
        _ = consensus.ProposeAsync(proposal, cancellationToken);
        var e1 = await exceptionOccurredTask.WaitAsync(WaitTimeout5, cancellationToken);
        var e2 = Assert.IsType<ArgumentException>(e1);
        Assert.StartsWith("Given proposal's proposer", e2.Message);
        Assert.Equal("proposal", e2.ParamName);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task Propose_Throw_With_InvalidRound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = Rand.GetRandom(output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        await using var consensus = new Net.Consensus.Consensus(Validators);

        await consensus.StartAsync(cancellationToken);

        var proposal = new ProposalBuilder
        {
            Round = 2,
            Block = blockchain.Propose(Signers[1]),
        }.Create(Signers[1]);
        var exceptionOccurredTask = consensus.ExceptionOccurred.WaitAsync();
        _ = consensus.ProposeAsync(proposal, cancellationToken);
        var e1 = await exceptionOccurredTask.WaitAsync(WaitTimeout5, cancellationToken);
        var e2 = Assert.IsType<ArgumentException>(e1);
        Assert.StartsWith("Given proposal's round", e2.Message);
        Assert.Equal("proposal", e2.ParamName);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    // proposal 이 늦게 올 수도 있다는 전제가 있어서 PreVote 단계에서 Timeout 를 검출해야 함
    public async Task TimeoutOccured_Propose_Without_Proposal()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = new ConsensusOptions
        {
            ProposeTimeoutBase = TimeSpan.FromSeconds(1),
            ProposeTimeoutDelta = TimeSpan.FromMilliseconds(100),
        };
        await using var consensus = new Net.Consensus.Consensus(
            height: 1,
            validators: Validators,
            options: options);

        var timeoutOccurredTask = consensus.TimeoutOccurred.WaitAsync();
        var stepChangedTask = consensus.StepChanged.WaitAsync();
        await consensus.StartAsync(cancellationToken);
        await timeoutOccurredTask.WaitAsync(
            options.TimeoutPropose(consensus.Round) + TimeSpan.FromMilliseconds(200), cancellationToken);
        await stepChangedTask.WaitAsync(
            options.TimeoutPropose(consensus.Round) + TimeSpan.FromMilliseconds(200), cancellationToken);
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(0, consensus.Round.Index);
    }
}
