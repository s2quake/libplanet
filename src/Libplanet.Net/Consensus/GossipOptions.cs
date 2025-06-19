using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus;

public sealed record class GossipOptions
{
    public ImmutableArray<Peer> Peers { get; init; } = [];

    public ImmutableArray<Peer> Seeds { get; init; } = [];

    public Action<MessageEnvelope> ValidateMessageToReceive { get; init; } = _ => { };

    public Action<IMessage> ValidateMessageToSend { get; init; } = _ => { };

    public Action<IMessage> ProcessMessage { get; init; } = _ => { };

    public TimeSpan RebuildTableInterval { get; init; } = TimeSpan.FromMinutes(1);

    public TimeSpan RefreshTableInterval { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan RefreshLifespan { get; init; } = TimeSpan.FromSeconds(60);

    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(1);
}
