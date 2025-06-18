using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus;

public sealed class ConsensusReactor : IAsyncDisposable
{
    private readonly Gossip _gossip;
    private readonly ConsensusContext _consensusContext;
    private bool _disposed;

    public ConsensusReactor(ITransport transport, Blockchain blockchain, ConsensusReactorOptions options)
    {
        var messageCommunicator =
            new MessageCommunicator(
                transport,
                options.ConsensusPeers,
                options.SeedPeers,
                ProcessMessage);
        _gossip = messageCommunicator.Gossip;

        _consensusContext = new ConsensusContext(
            messageCommunicator,
            blockchain,
            options.PrivateKey,
            options.TargetBlockInterval,
            options.ContextOptions);
    }

    public bool IsRunning { get; private set; }

    public int Height => _consensusContext.Height;

    public ImmutableArray<Peer> Validators => _gossip.Peers;

    internal ConsensusContext ConsensusContext => _consensusContext;

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await _gossip.DisposeAsync();
            await _consensusContext.DisposeAsync();
            _disposed = true;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsRunning)
        {
            throw new InvalidOperationException("Consensus reactor is already running.");
        }

        await _gossip.StartAsync(cancellationToken);
        _consensusContext.Start();
        IsRunning = true;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsRunning)
        {
            throw new InvalidOperationException("Consensus reactor is not running.");
        }

        await _consensusContext.DisposeAsync();
        await _gossip.StopAsync(cancellationToken);
        IsRunning = false;
    }

    public override string ToString()
    {
        var dict =
            JsonSerializer.Deserialize<Dictionary<string, object>>(
                _consensusContext.ToString()) ?? new Dictionary<string, object>();
        dict["peer"] = _gossip.AsPeer.ToString();

        return JsonSerializer.Serialize(dict);
    }

    private void ProcessMessage(IMessage message)
    {
        switch (message)
        {
            case ConsensusVoteSetBitsMessage voteSetBits:
                // Note: ConsensusVoteSetBitsMsg will not be stored to context's message log.
                var messages = _consensusContext.HandleVoteSetBits(voteSetBits.VoteSetBits);
                try
                {
                    var sender = _gossip.Peers.First(
                        peer => peer.Address.Equals(voteSetBits.Validator));
                    foreach (var msg in messages)
                    {
                        _gossip.PublishMessage(msg, new[] { sender });
                    }
                }
                catch (InvalidOperationException)
                {
                }

                break;

            case ConsensusMaj23Message maj23Message:
                try
                {
                    VoteSetBits? voteSetBits = _consensusContext.HandleMaj23(maj23Message.Maj23);
                    if (voteSetBits is null)
                    {
                        break;
                    }

                    var sender = _gossip.Peers.First(
                        peer => peer.Address.Equals(maj23Message.Validator));
                    _gossip.PublishMessage(
                        new ConsensusVoteSetBitsMessage { VoteSetBits = voteSetBits },
                        [sender]);
                }
                catch (InvalidOperationException)
                {
                }

                break;

            case ConsensusProposalClaimMessage proposalClaimmessage:
                try
                {
                    Proposal? proposal = _consensusContext.HandleProposalClaim(
                        proposalClaimmessage.ProposalClaim);
                    if (proposal is { } proposalNotNull)
                    {
                        var reply = new ConsensusProposalMessage { Proposal = proposalNotNull };
                        var sender = _gossip.Peers.First(
                            peer => peer.Address.Equals(proposalClaimmessage.Validator));

                        _gossip.PublishMessage(reply, new[] { sender });
                    }
                }
                catch (InvalidOperationException)
                {
                }

                break;

            case ConsensusMessage consensusMessage:
                _consensusContext.HandleMessage(consensusMessage);
                break;
        }
    }
}
