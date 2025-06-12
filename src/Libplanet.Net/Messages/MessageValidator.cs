using System.Globalization;
using Libplanet.Net.Options;
using Libplanet.Net.Transports;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

public sealed class MessageValidator
{
    private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.ffffffZ";
    private readonly ProtocolOptions _protocolOptions;

    internal MessageValidator(
        ProtocolOptions appProtocolVersionOptions,
        TimeSpan? messageTimestampBuffer)
    {
        _protocolOptions = appProtocolVersionOptions;
        MessageTimestampBuffer = messageTimestampBuffer;
    }

    public Protocol Protocol => _protocolOptions.Protocol;

    public ImmutableSortedSet<Address> AllowedSigners => _protocolOptions.AllowedSigners;

    public DifferentAppProtocolVersionEncountered DifferentApvEncountered
        => _protocolOptions.DifferentAppProtocolVersionEncountered;

    public TimeSpan? MessageTimestampBuffer { get; }

    public void ValidateProtocol(MessageEnvelope message)
        => ValidateProtocol(Protocol, AllowedSigners, DifferentApvEncountered, message);

    public void ValidateTimestamp(MessageEnvelope message)
        => ValidateTimestamp(MessageTimestampBuffer, DateTimeOffset.UtcNow, message.Timestamp);

    private static void ValidateProtocol(
        Protocol protocol,
        ImmutableSortedSet<Address> allowedSigners,
        DifferentAppProtocolVersionEncountered differentAppProtocolVersionEncountered,
        MessageEnvelope message)
    {
        if (message.Protocol.Equals(protocol))
        {
            return;
        }

        bool trusted = !allowedSigners.All(
            publicKey => !message.Protocol.Verify());

        if (trusted)
        {
            differentAppProtocolVersionEncountered(
                message.Remote, message.Protocol, protocol);
        }

        if (!trusted || !message.Protocol.Version.Equals(protocol.Version))
        {
            throw new InvalidProtocolException(
                $"The APV of a received message is invalid:\n" +
                $"Expected: APV {protocol} with " +
                $"signature {ByteUtility.Hex(protocol.Signature)} by " +
                $"signer {protocol.Signer}\n" +
                $"Actual: APV {message.Protocol} with " +
                $"signature: {ByteUtility.Hex(message.Protocol.Signature)} by " +
                $"signer: {message.Protocol.Signer}\n" +
                $"Signed by a trusted signer: {trusted}",
                protocol,
                message.Protocol,
                trusted);
        }
    }

    private static void ValidateTimestamp(
        TimeSpan? timestampBuffer,
        DateTimeOffset currentTimestamp,
        DateTimeOffset messageTimestamp)
    {
        if (timestampBuffer is TimeSpan buffer &&
            (currentTimestamp - messageTimestamp).Duration() > buffer)
        {
            var cultureInfo = CultureInfo.InvariantCulture;
            throw new InvalidMessageTimestampException(
                $"The timestamp of a received message is invalid:\n" +
                $"Message timestamp buffer: {buffer}\n" +
                $"Current timestamp: " +
                $"{currentTimestamp.ToString(TimestampFormat, cultureInfo)}\n" +
                $"Message timestamp: " +
                $"{messageTimestamp.ToString(TimestampFormat, cultureInfo)}",
                messageTimestamp,
                buffer,
                currentTimestamp);
        }
    }
}
