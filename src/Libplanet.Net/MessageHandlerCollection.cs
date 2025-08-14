namespace Libplanet.Net;

public sealed class MessageHandlerCollection : MessageComponentCollectionBase<IMessageHandler>
{
    public MessageHandlerCollection()
        : base(m => m.MessageType)
    {
    }
}
