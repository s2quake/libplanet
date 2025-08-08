using System.Reactive.Linq;
using Libplanet.Extensions;
using Libplanet.Net.Consensus;
using Libplanet.TestUtilities.Extensions;

namespace Libplanet.Net.Tests.Consensus;

public sealed class ConsensusTest
{
    private const int Timeout = 30000;

    [Fact(Timeout = Timeout)]
    public async Task BaseTest()
    {
        await using var consensus = new Net.Consensus.Consensus(
            height: 1,
            validators: TestUtils.Validators,
            options: new ConsensusOptions());

        Assert.Equal(1, consensus.Height);
        Assert.Throws<InvalidOperationException>(() => consensus.Round);
        Assert.Equal(ConsensusStep.Default, consensus.Step);
        Assert.Null(consensus.Proposal);
        Assert.Equal(TestUtils.Validators, consensus.Validators);
    }

    [Theory(Timeout = Timeout)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public async Task StartAsync(int index, bool isProposer)
    {
        var blockchain = TestUtils.MakeBlockchain();
        await using var consensus = new Net.Consensus.Consensus(
            height: 1,
            // signer: TestUtils.PrivateKeys[index].AsSigner(),
            validators: TestUtils.Validators,
            options: new ConsensusOptions());
        var controller = new ConsensusController(
            TestUtils.PrivateKeys[index].AsSigner(),
            consensus,
            blockchain);
        // var tcs = new TaskCompletionSource();
        // using var _ = consensus.ShouldPropose.Subscribe(_ => tcs.SetResult());
        var proposedTask = controller.Proposed.WaitAsync();

        await consensus.StartAsync(default);

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
            await Assert.ThrowsAsync<TimeoutException>(() => proposedTask.WaitAsync(TimeSpan.FromSeconds(1)));
        }
    }

    [Fact(Timeout = Timeout)]
    public async Task StopAsync()
    {
        var blockchain = TestUtils.MakeBlockchain();
        await using var consensus = new Net.Consensus.Consensus(
            height: 1,
            // signer: TestUtils.PrivateKeys[1].AsSigner(),
            validators: TestUtils.Validators,
            options: new ConsensusOptions());

        await consensus.StartAsync(default);
        await consensus.StopAsync(default);

        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round.Index);
        Assert.Equal(ConsensusStep.Default, consensus.Step);
        Assert.Null(consensus.Proposal);
    }

    [Fact(Timeout = Timeout)]
    public async Task DisposeAsync()
    {
        var blockchain = TestUtils.MakeBlockchain();
        var consensus1 = new Net.Consensus.Consensus(
            height: 1,
            // signer: TestUtils.PrivateKeys[1].AsSigner(),
            validators: TestUtils.Validators,
            options: new ConsensusOptions());
        var consensus2 = new Net.Consensus.Consensus(
            height: 1,
            // signer: TestUtils.PrivateKeys[1].AsSigner(),
            validators: TestUtils.Validators,
            options: new ConsensusOptions());

        await consensus1.StartAsync(default);
        await consensus1.DisposeAsync();
        await consensus2.DisposeAsync();

        Assert.True(consensus1.IsDisposed);
        Assert.True(consensus2.IsDisposed);

        await consensus1.DisposeAsync();
        await consensus2.DisposeAsync();
    }

    [Fact(Timeout = Timeout)]
    public async Task Propose()
    {
        var blockchain = TestUtils.MakeBlockchain();
        var options = new ConsensusOptions
        {
            ProposeTimeoutBase = TimeSpan.FromSeconds(1),
            ProposeTimeoutDelta = TimeSpan.FromMilliseconds(100),
        };
        await using var consensus = new Net.Consensus.Consensus(
            height: 1,
            // signer: TestUtils.PrivateKeys[0].AsSigner(),
            validators: TestUtils.Validators,
            options: options);

        Assert.Equal(ConsensusStep.Default, consensus.Step);

        await consensus.StartAsync(default);
        Assert.Equal(ConsensusStep.Propose, consensus.Step);

        var proposal = new ProposalBuilder
        {
            Block = blockchain.ProposeBlock(TestUtils.PrivateKeys[1]),
        }.Create(TestUtils.PrivateKeys[1]);
        consensus.Propose(proposal);
        await consensus.StepChanged.WaitAsync().WaitAsync(options.TimeoutPropose(consensus.Round));
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(proposal, consensus.Proposal);
    }

    [Fact(Timeout = Timeout)]
    public async Task Propose_Throw_Double_Proposal()
    {
        var blockchain = TestUtils.MakeBlockchain();
        var options = new ConsensusOptions
        {
            ProposeTimeoutBase = TimeSpan.FromSeconds(2),
            ProposeTimeoutDelta = TimeSpan.FromMilliseconds(100),
        };
        await using var consensus = new Net.Consensus.Consensus(
            height: 1,
            // signer: TestUtils.PrivateKeys[0].AsSigner(),
            validators: TestUtils.Validators,
            options: options);

        await consensus.StartAsync(default);

        var proposal = new ProposalBuilder
        {
            Block = blockchain.ProposeBlock(TestUtils.PrivateKeys[1]),
        }.Create(TestUtils.PrivateKeys[1]);
        var stepChangedTask = consensus.StepChanged.WaitAsync();
        consensus.Propose(proposal);

        TestUtils.InvokeDelay(() => consensus.Propose(proposal), 100);
        var e1 = await consensus.ExceptionOccurred.WaitAsync().WaitAsync(TimeSpan.FromSeconds(2));
        var e2 = Assert.IsType<InvalidOperationException>(e1);
        Assert.StartsWith("Proposal already exists", e2.Message);

        await stepChangedTask.WaitAsync(options.TimeoutPropose(consensus.Round));
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
    }

    [Fact(Timeout = Timeout)]
    public async Task Propose_Throw_With_InvalidProposer()
    {
        var blockchain = TestUtils.MakeBlockchain();
        await using var consensus = new Net.Consensus.Consensus(
            height: 1,
            // signer: TestUtils.PrivateKeys[0].AsSigner(),
            validators: TestUtils.Validators,
            options: new());

        await consensus.StartAsync(default);

        var proposal = new ProposalBuilder
        {
            Block = blockchain.ProposeBlock(TestUtils.PrivateKeys[0]),
        }.Create(TestUtils.PrivateKeys[0]);
        TestUtils.InvokeDelay(() => consensus.Propose(proposal), 100);
        var e1 = await consensus.ExceptionOccurred.WaitAsync().WaitAsync(TimeSpan.FromSeconds(2));
        var e2 = Assert.IsType<ArgumentException>(e1);
        Assert.StartsWith("Given proposal's proposer", e2.Message);
        Assert.Equal("proposal", e2.ParamName);
    }

    [Fact(Timeout = Timeout)]
    public async Task Propose_Throw_With_InvalidRound()
    {
        var blockchain = TestUtils.MakeBlockchain();
        await using var consensus = new Net.Consensus.Consensus(
            height: 1,
            // signer: TestUtils.PrivateKeys[0].AsSigner(),
            validators: TestUtils.Validators,
            options: new());

        await consensus.StartAsync(default);

        var proposal = new ProposalBuilder
        {
            Round = 2,
            Block = blockchain.ProposeBlock(TestUtils.PrivateKeys[1]),
        }.Create(TestUtils.PrivateKeys[0]);
        TestUtils.InvokeDelay(() => consensus.Propose(proposal), 100);
        var e1 = await consensus.ExceptionOccurred.WaitAsync().WaitAsync(TimeSpan.FromSeconds(2));
        var e2 = Assert.IsType<ArgumentException>(e1);
        Assert.StartsWith("Given proposal's round", e2.Message);
        Assert.Equal("proposal", e2.ParamName);
    }

    [Theory(Timeout = Timeout)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task TimeoutOccured_Propose_Without_Proposal(int index)
    {
        var blockchain = TestUtils.MakeBlockchain();
        var options = new ConsensusOptions
        {
            ProposeTimeoutBase = TimeSpan.FromSeconds(1),
            ProposeTimeoutDelta = TimeSpan.FromMilliseconds(100),
        };
        await using var consensus = new Net.Consensus.Consensus(
            height: 1,
            // signer: TestUtils.PrivateKeys[index].AsSigner(),
            validators: TestUtils.Validators,
            options: options);

        var timeoutOccurredTask = consensus.TimeoutOccurred.WaitAsync();
        var stepChangedTask = consensus.StepChanged.WaitAsync();
        await consensus.StartAsync(default);
        await timeoutOccurredTask.WaitAsync(options.TimeoutPropose(consensus.Round));
        await stepChangedTask.WaitAsync(options.TimeoutPropose(consensus.Round));
        Assert.Equal(ConsensusStep.Propose, consensus.Step);
        Assert.Equal(1, consensus.Round.Index);
    }
}
