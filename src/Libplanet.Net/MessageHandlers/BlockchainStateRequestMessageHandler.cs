using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class BlockchainStateRequestMessageHandler : MessageHandlerBase<BlockchainStateRequestMessage>, IDisposable
{
    private readonly Blockchain _blockchain;
    private readonly ITransport _transport;
    private readonly IDisposable _subscription;
    private BlockchainStateResponseMessage _response;

    public BlockchainStateRequestMessageHandler(Blockchain blockchain, ITransport transport)
    {
        _blockchain = blockchain;
        _transport = transport;
        _response = CreateResponse(_blockchain);
        _subscription = _blockchain.TipChanged.Subscribe(e =>
        {
            _response = CreateResponse(_blockchain);
        });
    }

    public BlockchainStateRequestMessageHandler(Swarm swarm)
        : this(swarm.Blockchain, swarm.Transport)
    {
    }

    public void Dispose() => _subscription.Dispose();

    protected override void OnHandle(BlockchainStateRequestMessage message, MessageEnvelope messageEnvelope)
        => _transport.Post(messageEnvelope.Sender, _response, messageEnvelope.Identity);

    private static BlockchainStateResponseMessage CreateResponse(Blockchain blockchain)
    {
        var tip = blockchain.Tip;
        return new BlockchainStateResponseMessage
        {
            Genesis = blockchain.Genesis,
            Tip = tip,
        };
    }
}
