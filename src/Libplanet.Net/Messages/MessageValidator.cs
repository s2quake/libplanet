// using Libplanet.Net.Options;
// using Libplanet.Net.Transports;
// using Libplanet.Types;

// namespace Libplanet.Net.Messages;

// public sealed class MessageValidator
// {
//     private readonly ProtocolOptions _protocolOptions;

//     internal MessageValidator(ProtocolOptions protocolOptions)
//     {
//         _protocolOptions = protocolOptions;
//     }

//     public Protocol Protocol => _protocolOptions.Protocol;

//     public void ValidateProtocol(MessageEnvelope messageEnvelop)
//         => ValidateProtocol(Protocol, messageEnvelop);

//     public void ValidateTimestamp(MessageEnvelope messageEnvelop)
//     {
//         messageEnvelop.Validate(_protocolOptions.MessageLifetime);
//     }

//     private static void ValidateProtocol(
//         Protocol protocol,
//         MessageEnvelope message)
//     {
//         if (!message.Protocol.Equals(protocol))
//         {
//             throw new ArgumentException(
//                 "The protocol of the message is the same as the expected one.",
//                 nameof(message));
//         }

//         // bool trusted = !allowedSigners.All(
//         //     publicKey => !message.Protocol.Verify());


//         // if (!trusted || !message.Protocol.Version.Equals(protocol.Version))
//         // {
//         //     throw new InvalidProtocolException(
//         //         $"The APV of a received message is invalid:\n" +
//         //         $"Expected: APV {protocol} with " +
//         //         $"signature {ByteUtility.Hex(protocol.Signature)} by " +
//         //         $"signer {protocol.Signer}\n" +
//         //         $"Actual: APV {message.Protocol} with " +
//         //         $"signature: {ByteUtility.Hex(message.Protocol.Signature)} by " +
//         //         $"signer: {message.Protocol.Signer}\n" +
//         //         $"Signed by a trusted signer: {trusted}",
//         //         protocol,
//         //         message.Protocol,
//         //         trusted);
//         // }
//     }

//     // private static void ValidateTimestamp(
//     //     TimeSpan? timestampBuffer, DateTimeOffset currentTimestamp, DateTimeOffset messageTimestamp)
//     // {
//     //     if (timestampBuffer is TimeSpan buffer && (currentTimestamp - messageTimestamp).Duration() > buffer)
//     //     {
//     //         throw new InvalidMessageTimestampException(messageTimestamp, buffer, currentTimestamp);
//     //     }
//     // }
// }
