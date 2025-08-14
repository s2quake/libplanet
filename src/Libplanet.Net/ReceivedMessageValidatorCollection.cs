namespace Libplanet.Net;

public sealed class ReceivedMessageValidatorCollection : MessageComponentCollectionBase<IReceivedMessageValidator>
{
    public ReceivedMessageValidatorCollection()
        : base(m => m.MessageType)
    {
    }
}
