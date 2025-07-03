namespace Libplanet.Net.Messages;

public interface IMessage
{
    MessageId Id { get; }

    bool HasNext { get; init; }
}
