namespace Libplanet.Net;

public interface IMessageRouter
{
    IObservable<(IMessageHandler Handler, Exception Exception)> MessageHandlingFailed { get; }

    IObservable<(ISendingMessageValidator Validator, Exception Exception)> SendingMessageValidationFailed { get; }

    IObservable<(IReceivedMessageValidator Validator, Exception Exception)> ReceivedMessageValidationFailed { get; }

    IDisposable Register(IMessageHandler handler);

    IDisposable Register(ISendingMessageValidator validator);

    IDisposable Register(IReceivedMessageValidator validator);
}
