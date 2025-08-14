using System.Reactive.Subjects;
using Libplanet.Net.Messages;
using Libplanet.Types.Threading;

namespace Libplanet.Net;

public sealed class MessageRouter : IAsyncDisposable, IMessageRouter
{
    private readonly Subject<(IMessageHandler, Exception)> _messageHandlingFailed = new();
    private readonly Subject<(ISendingMessageValidator, Exception)> _sendingMessageValidationFailedSubject = new();
    private readonly Subject<(IReceivedMessageValidator, Exception)> _receivedMessageValidationFailedSubject = new();
    private readonly MessageHandlerCollection _handlers = [];
    private readonly SendingMessageValidatorCollection _sendingValidators = [];
    private readonly ReceivedMessageValidatorCollection _receivedValidators = [];
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);

    public IObservable<(IMessageHandler, Exception)> MessageHandlingFailed => _messageHandlingFailed;

    public IObservable<(ISendingMessageValidator, Exception)> SendingMessageValidationFailed
        => _sendingMessageValidationFailedSubject;

    public IObservable<(IReceivedMessageValidator, Exception)> ReceivedMessageValidationFailed
        => _receivedMessageValidationFailedSubject;

    public async ValueTask DisposeAsync()
    {
        _messageHandlingFailed.Dispose();
        _sendingMessageValidationFailedSubject.Dispose();
        _receivedMessageValidationFailedSubject.Dispose();
        _lock.Dispose();
        await ValueTask.CompletedTask;
    }

    public IDisposable Register(IMessageHandler handler)
    {
        using var _ = _lock.WriteScope();
        _handlers.Add(handler);
        return new Unregister(() => Remove(handler));
    }

    public IDisposable Register(ISendingMessageValidator validator)
    {
        using var _ = _lock.WriteScope();
        _sendingValidators.Add(validator);
        return new Unregister(() => Remove(validator));
    }

    public IDisposable Register(IReceivedMessageValidator validator)
    {
        using var _ = _lock.WriteScope();
        _receivedValidators.Add(validator);
        return new Unregister(() => Remove(validator));
    }

    public bool Contains(Type messageType)
    {
        using var _ = new ReadScope(_lock);
        return _handlers.Contains(messageType);
    }

    public bool VerifySendingMessagre(MessageEnvelope messageEnvelope)
    {
        var validators = GetValidators();
        foreach (var validator in validators)
        {
            try
            {
                validator.Validate(messageEnvelope);
            }
            catch (Exception e)
            {
                _sendingMessageValidationFailedSubject.OnNext((validator, e));
                return false;
            }
        }

        return true;

        ImmutableArray<ISendingMessageValidator> GetValidators()
        {
            using var _ = new ReadScope(_lock);
            return _sendingValidators.GetAll(messageEnvelope.MessageType);
        }
    }

    public bool VerifyReceivedMessage(MessageEnvelope messageEnvelope)
    {
        var validators = GetValidators();
        foreach (var validator in validators)
        {
            try
            {
                validator.Validate(messageEnvelope);
            }
            catch (Exception e)
            {
                _receivedMessageValidationFailedSubject.OnNext((validator, e));
                return false;
            }
        }

        return true;

        ImmutableArray<IReceivedMessageValidator> GetValidators()
        {
            using var _ = new ReadScope(_lock);
            return _receivedValidators.GetAll(messageEnvelope.MessageType);
        }
    }

    public async Task HandleAsync(MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        var message = messageEnvelope.Message;
        var messageType = message.GetType();
        var handlers = GetHandlers(messageType);

        await Parallel.ForEachAsync(handlers, cancellationToken, async (handler, cancellationToken) =>
        {
            try
            {
                await handler.HandleAsync(messageEnvelope, cancellationToken);
            }
            catch (Exception e)
            {
                _messageHandlingFailed.OnNext((handler, e));
            }
        });

        ImmutableArray<IMessageHandler> GetHandlers(Type messageType)
        {
            using var _ = new ReadScope(_lock);
            return _handlers.GetAll(messageType);
        }
    }

    private void Remove(IMessageHandler handler)
    {
        using var _ = _lock.WriteScope();
        _handlers.Remove(handler);
    }

    private void Remove(ISendingMessageValidator validator)
    {
        using var _ = _lock.WriteScope();
        _sendingValidators.Remove(validator);
    }

    private void Remove(IReceivedMessageValidator validator)
    {
        using var _ = _lock.WriteScope();
        _receivedValidators.Remove(validator);
    }

    private sealed class Unregister(Action action)
        : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                action();
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
