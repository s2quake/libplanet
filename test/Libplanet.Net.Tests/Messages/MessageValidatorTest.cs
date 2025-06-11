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
            var peer = new Peer { Address = new PrivateKey().Address, EndPoint = new DnsEndPoint("0.0.0.0", 0) };
            var buffer = TimeSpan.FromSeconds(1);
            var apvOption = new AppProtocolVersionOptions();
            var messageValidator = new MessageValidator(apvOption, buffer);

            // Within buffer window is okay.
            messageValidator.ValidateTimestamp(
                new Message
                {
                    Content = new PingMessage(),
                    Protocol = apvOption.AppProtocolVersion,
                    Remote = peer,
                    Timestamp = DateTimeOffset.UtcNow + buffer.Divide(2),
                });
            messageValidator.ValidateTimestamp(
                new Message
                {
                    Content = new PingMessage(),
                    Protocol = apvOption.AppProtocolVersion,
                    Remote = peer,
                    Timestamp = DateTimeOffset.UtcNow - buffer.Divide(2),
                });

            // Outside buffer throws an exception.
            Assert.Throws<InvalidMessageTimestampException>(() =>
                messageValidator.ValidateTimestamp(
                    new Message
                    {
                        Content = new PingMessage(),
                        Protocol = apvOption.AppProtocolVersion,
                        Remote = peer,
                        Timestamp = DateTimeOffset.UtcNow + buffer.Multiply(2),
                    }));
            Assert.Throws<InvalidMessageTimestampException>(() =>
                messageValidator.ValidateTimestamp(
                    new Message
                    {
                        Content = new PingMessage(),
                        Protocol = apvOption.AppProtocolVersion,
                        Remote = peer,
                        Timestamp = DateTimeOffset.UtcNow - buffer.Multiply(2),
                    }));

            // If buffer is null, no exception gets thrown.
            messageValidator = new MessageValidator(apvOption, null);
            messageValidator.ValidateTimestamp(
                new Message
                {
                    Content = new PingMessage(),
                    Protocol = apvOption.AppProtocolVersion,
                    Remote = peer,
                    Timestamp = DateTimeOffset.MaxValue,
                });
            messageValidator.ValidateTimestamp(
                new Message
                {
                    Content = new PingMessage(),
                    Protocol = apvOption.AppProtocolVersion,
                    Remote = peer,
                    Timestamp = DateTimeOffset.MinValue,
                });
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
            var peer = new Peer { Address = trustedSigner.Address, EndPoint = new DnsEndPoint("0.0.0.0", 0) };

            // Apv
            var trustedApv = ProtocolVersion.Create(trustedSigner, version1, extra1);
            var trustedDifferentVersionApv = ProtocolVersion.Create(
                trustedSigner, version2, extra1);
            var trustedDifferentExtraApv = ProtocolVersion.Create(trustedSigner, version1, extra2);
            var unknownApv = ProtocolVersion.Create(unknownSigner, version1, extra1);
            var unknownDifferentVersionApv = ProtocolVersion.Create(
                unknownSigner, version2, extra1);
            var unknownDifferentExtraApv = ProtocolVersion.Create(unknownSigner, version1, extra2);

            // Signer
            ImmutableHashSet<PublicKey>? trustedApvSigners =
                new HashSet<PublicKey>() { trustedSigner.PublicKey }.ToImmutableHashSet();
            ImmutableHashSet<PublicKey>? emptyApvSigners =
                new HashSet<PublicKey>() { }.ToImmutableHashSet();

            // Ping
            var trustedPing = new Message
            {
                Content = new PingMessage(),
                Protocol = trustedApv,
                Remote = peer,
                Timestamp = DateTimeOffset.UtcNow,
            };
            var trustedDifferentVersionPing = new Message
            {
                Content = new PingMessage(),
                Protocol = trustedDifferentVersionApv,
                Remote = peer,
                Timestamp = DateTimeOffset.UtcNow,
            };
            var trustedDifferentExtraPing = new Message
            {
                Content = new PingMessage(),
                Protocol = trustedDifferentExtraApv,
                Remote = peer,
                Timestamp = DateTimeOffset.UtcNow,
            };
            var unknownPing = new Message
            {
                Content = new PingMessage(),
                Protocol = unknownApv,
                Remote = peer,
                Timestamp = DateTimeOffset.UtcNow,
            };
            var unknownDifferentVersionPing = new Message
            {
                Content = new PingMessage(),
                Protocol = unknownDifferentVersionApv,
                Remote = peer,
                Timestamp = DateTimeOffset.UtcNow,
            };
            var unknownDifferentExtraPing = new Message
            {
                Content = new PingMessage(),
                Protocol = unknownDifferentExtraApv,
                Remote = peer,
                Timestamp = DateTimeOffset.UtcNow,
            };

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
