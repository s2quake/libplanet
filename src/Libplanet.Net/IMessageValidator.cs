namespace Libplanet.Net;

public interface IMessageValidator
{
    Type MessageType { get; }

    void Validate(IMessage message);
}
