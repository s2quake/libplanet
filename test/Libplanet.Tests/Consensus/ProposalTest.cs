using Libplanet.Consensus;
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Tests.Store;
using Serilog;
using Xunit;
using Xunit.Abstractions;

namespace Libplanet.Tests.Consensus
{
    public class ProposalTest
    {
        private ILogger _logger;

        public ProposalTest(ITestOutputHelper output)
        {
            const string outputTemplate =
                "{Timestamp:HH:mm:ss:ffffffZ} - {Message}";
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.TestOutput(output, outputTemplate: outputTemplate)
                .CreateLogger()
                .ForContext<ProposalTest>();

            _logger = Log.ForContext<ProposalTest>();
        }

        [Fact]
        public void InvalidSignature()
        {
            MemoryStoreFixture fx = new MemoryStoreFixture();
            var codec = new Codec();

            ProposalMetadata metadata = new ProposalMetadata(
                1,
                0,
                DateTimeOffset.UtcNow,
                new PrivateKey().PublicKey,
                ModelSerializer.SerializeToBytes(fx.Block1),
                -1);

            // Empty Signature
            var emptySigBencodex = metadata.Encoded.Add(Proposal.SignatureKey, Array.Empty<byte>());
            Assert.Throws<ArgumentNullException>(() => new Proposal(emptySigBencodex));

            // Invalid Signature
            var invSigBencodex = metadata.Encoded.Add(
                Proposal.SignatureKey,
                new PrivateKey().Sign(ModelSerializer.SerializeToBytes(fx.Block2)));
            Assert.Throws<ArgumentException>(() => new Proposal(invSigBencodex));
        }

        [Fact]
        public void Sign()
        {
            MemoryStoreFixture fx = new MemoryStoreFixture();
            var codec = new Codec();
            var key = new PrivateKey();

            ProposalMetadata metadata = new ProposalMetadata(
                1,
                0,
                DateTimeOffset.UtcNow,
                key.PublicKey,
                ModelSerializer.SerializeToBytes(fx.Block1),
                -1);
            Proposal proposal = metadata.Sign(key);

            TestUtils.AssertBytesEqual(proposal.Signature, key.Sign(metadata.ByteArray));
            Assert.True(key.PublicKey.Verify(metadata.ByteArray, proposal.Signature));
        }
    }
}
