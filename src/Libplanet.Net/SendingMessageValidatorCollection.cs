namespace Libplanet.Net;

public sealed class SendingMessageValidatorCollection : MessageComponentCollectionBase<ISendingMessageValidator>
{
    public SendingMessageValidatorCollection()
        : base(m => m.MessageType)
    {
    }
}
