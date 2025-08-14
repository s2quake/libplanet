// using System.Collections;
// using System.Diagnostics.CodeAnalysis;

// namespace Libplanet.Net;

// public sealed class MessageValidatorCollection
//     : IEnumerable<IMessageValidator>, IDisposable
// {
//     private readonly Dictionary<Type, IMessageValidator> _validatorByType = [];

//     public int Count => _validatorByType.Count;

//     public IMessageValidator this[Type messageType] => _validatorByType[messageType];

//     public void Add(IMessageValidator validator)
//     {
//         if (_validatorByType.ContainsKey(validator.MessageType))
//         {
//             throw new InvalidOperationException(
//                 $"Handler for message type {validator.MessageType} already exists.");
//         }

//         _validatorByType.Add(validator.MessageType, validator);
//     }

//     public void AddRange(IEnumerable<IMessageValidator> validators)
//     {
//         foreach (var validator in validators)
//         {
//             Add(validator);
//         }
//     }

//     public bool Remove(IMessageValidator validator)
//     {
//         if (!_validatorByType.Remove(validator.MessageType))
//         {
//             return false;
//         }

//         return true;
//     }

//     public void RemoveRange(IEnumerable<IMessageValidator> validators)
//     {
//         foreach (var validator in validators)
//         {
//             Remove(validator);
//         }
//     }

//     public bool Contains(Type messageType) => _validatorByType.ContainsKey(messageType);

//     public bool TryGetValidator(Type messageType, [MaybeNullWhen(false)] out IMessageValidator validator)
//         => _validatorByType.TryGetValue(messageType, out validator);

//     public void Validate(IMessage message)
//     {
//         var messageType = message.GetType();
//         while (messageType is not null && typeof(IMessage).IsAssignableFrom(messageType))
//         {
//             if (TryGetValidator(messageType, out var validator))
//             {
//                 validator.Validate(message);
//                 return;
//             }

//             messageType = messageType.BaseType;
//         }

//         if (TryGetValidator(typeof(IMessage), out var validator2))
//         {
//             validator2.Validate(message);
//         }
//     }

//     public IEnumerator<IMessageValidator> GetEnumerator()
//     {
//         foreach (var validator in _validatorByType.Values)
//         {
//             yield return validator;
//         }
//     }

//     IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

//     public void Dispose()
//     {
//         foreach (var validator in _validatorByType.Values)
//         {
//             if (validator is IDisposable disposable)
//             {
//                 disposable.Dispose();
//             }
//         }
//     }
// }
