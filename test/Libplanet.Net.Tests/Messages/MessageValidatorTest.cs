using System.Net;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.Transports;
using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Tests.Messages
{
    public class MessageValidatorTest
    {
        [Fact]
        public void ValidateTimestamp()
        {
            var peer = new BoundPeer(new PrivateKey().PublicKey, new DnsEndPoint("0.0.0.0", 0));
            var buffer = TimeSpan.FromSeconds(1);
            var apvOption = new AppProtocolVersionOptions();
            var messageValidator = new MessageValidator(apvOption, buffer);

            // Within buffer window is okay.
            messageValidator.ValidateTimestamp(
                new Message(
                    new PingMessage(),
                    apvOption.AppProtocolVersion,
                    peer,
                    DateTimeOffset.UtcNow + buffer.Divide(2),
                    null));
            messageValidator.ValidateTimestamp(
                new Message(
                    new PingMessage(),
                    apvOption.AppProtocolVersion,
                    peer,
                    DateTimeOffset.UtcNow - buffer.Divide(2),
                    null));

            // Outside buffer throws an exception.
            Assert.Throws<InvalidMessageTimestampException>(() =>
                messageValidator.ValidateTimestamp(
                    new Message(
                        new PingMessage(),
                        apvOption.AppProtocolVersion,
                        peer,
                        DateTimeOffset.UtcNow + buffer.Multiply(2),
                        null)));
            Assert.Throws<InvalidMessageTimestampException>(() =>
                messageValidator.ValidateTimestamp(
                    new Message(
                        new PingMessage(),
                        apvOption.AppProtocolVersion,
                        peer,
                        DateTimeOffset.UtcNow - buffer.Multiply(2),
                        null)));

            // If buffer is null, no exception gets thrown.
            messageValidator = new MessageValidator(apvOption, null);
            messageValidator.ValidateTimestamp(
                new Message(
                    new PingMessage(),
                    apvOption.AppProtocolVersion,
                    peer,
                    DateTimeOffset.MaxValue,
                    null));
            messageValidator.ValidateTimestamp(
                new Message(
                    new PingMessage(),
                    apvOption.AppProtocolVersion,
                    peer,
                    DateTimeOffset.MinValue,
                    null));
        }

        [Fact]
        public void ValidateAppProtocolVersion()
        {
            var random = new Random();
            var identity = new byte[16];
            random.NextBytes(identity);
            var called = false;
            var trustedSigner = new PrivateKey();
            var unknownSigner = new PrivateKey();
            var version1 = 1;
            var version2 = 2;
            var extra1 = ModelSerializer.SerializeToBytes(13);
            var extra2 = ModelSerializer.SerializeToBytes(17);

            DifferentAppProtocolVersionEncountered callback = (p, pv, lv) => { called = true; };
            var peer = new BoundPeer(trustedSigner.PublicKey, new DnsEndPoint("0.0.0.0", 0));

            // Apv
            var trustedApv = Protocol.Create(trustedSigner, version1, extra1);
            var trustedDifferentVersionApv = Protocol.Create(
                trustedSigner, version2, extra1);
            var trustedDifferentExtraApv = Protocol.Create(trustedSigner, version1, extra2);
            var unknownApv = Protocol.Create(unknownSigner, version1, extra1);
            var unknownDifferentVersionApv = Protocol.Create(
                unknownSigner, version2, extra1);
            var unknownDifferentExtraApv = Protocol.Create(unknownSigner, version1, extra2);

            // Signer
            ImmutableHashSet<PublicKey>? trustedApvSigners =
                new HashSet<PublicKey>() { trustedSigner.PublicKey }.ToImmutableHashSet();
            ImmutableHashSet<PublicKey>? emptyApvSigners =
                new HashSet<PublicKey>() { }.ToImmutableHashSet();

            // Ping
            var trustedPing = new Message(
                new PingMessage(),
                trustedApv,
                peer,
                DateTimeOffset.UtcNow,
                null);
            var trustedDifferentVersionPing = new Message(
                new PingMessage(),
                trustedDifferentVersionApv,
                peer,
                DateTimeOffset.UtcNow,
                null);
            var trustedDifferentExtraPing = new Message(
                new PingMessage(),
                trustedDifferentExtraApv,
                peer,
                DateTimeOffset.UtcNow,
                null);
            var unknownPing = new Message(
                new PingMessage(),
                unknownApv,
                peer,
                DateTimeOffset.UtcNow,
                null);
            var unknownDifferentVersionPing = new Message(
                new PingMessage(),
                unknownDifferentVersionApv,
                peer,
                DateTimeOffset.UtcNow,
                null);
            var unknownDifferentExtraPing = new Message(
                new PingMessage(),
                unknownDifferentExtraApv,
                peer,
                DateTimeOffset.UtcNow,
                null);

            DifferentAppProtocolVersionException exception;
            AppProtocolVersionOptions appProtocolVersionOptions;
            MessageValidator messageValidator;

            // Trust specific signers.
            appProtocolVersionOptions = new AppProtocolVersionOptions()
            {
                AppProtocolVersion = trustedApv,
                TrustedAppProtocolVersionSigners = trustedApvSigners,
                DifferentAppProtocolVersionEncountered = callback,
            };

            messageValidator = new MessageValidator(appProtocolVersionOptions, null);

            // Check trust pings
            messageValidator.ValidateAppProtocolVersion(trustedPing);
            Assert.False(called);
            exception = Assert.Throws<DifferentAppProtocolVersionException>(
                () => messageValidator.ValidateAppProtocolVersion(trustedDifferentVersionPing));
            Assert.True(exception.Trusted);
            Assert.True(called);
            called = false;
            messageValidator.ValidateAppProtocolVersion(trustedDifferentExtraPing);
            Assert.True(called);
            called = false;

            // Check unknown pings
            exception = Assert.Throws<DifferentAppProtocolVersionException>(
                () => messageValidator.ValidateAppProtocolVersion(unknownPing));
            Assert.False(exception.Trusted);
            Assert.False(called);
            exception = Assert.Throws<DifferentAppProtocolVersionException>(
                () => messageValidator.ValidateAppProtocolVersion(unknownDifferentVersionPing));
            Assert.False(exception.Trusted);
            Assert.False(called);
            exception = Assert.Throws<DifferentAppProtocolVersionException>(
                () => messageValidator.ValidateAppProtocolVersion(unknownDifferentExtraPing));
            Assert.False(exception.Trusted);
            Assert.False(called);

            // Trust no one.
            appProtocolVersionOptions = new AppProtocolVersionOptions()
            {
                AppProtocolVersion = trustedApv,
                TrustedAppProtocolVersionSigners = emptyApvSigners,
                DifferentAppProtocolVersionEncountered = callback,
            };

            messageValidator = new MessageValidator(appProtocolVersionOptions, null);

            // Check trust pings
            messageValidator.ValidateAppProtocolVersion(trustedPing);
            Assert.False(called);
            exception = Assert.Throws<DifferentAppProtocolVersionException>(
                () => messageValidator.ValidateAppProtocolVersion(trustedDifferentVersionPing));
            Assert.False(exception.Trusted);
            Assert.False(called);
            exception = Assert.Throws<DifferentAppProtocolVersionException>(
                () => messageValidator.ValidateAppProtocolVersion(trustedDifferentExtraPing));
            Assert.False(exception.Trusted);
            Assert.False(called);

            // Check unknown pings
            exception = Assert.Throws<DifferentAppProtocolVersionException>(
                () => messageValidator.ValidateAppProtocolVersion(unknownPing));
            Assert.False(exception.Trusted);
            Assert.False(called);
            exception = Assert.Throws<DifferentAppProtocolVersionException>(
                () => messageValidator.ValidateAppProtocolVersion(unknownDifferentVersionPing));
            Assert.False(exception.Trusted);
            Assert.False(called);
            exception = Assert.Throws<DifferentAppProtocolVersionException>(
                () => messageValidator.ValidateAppProtocolVersion(unknownDifferentExtraPing));
            Assert.False(exception.Trusted);
            Assert.False(called);
        }
    }
}
