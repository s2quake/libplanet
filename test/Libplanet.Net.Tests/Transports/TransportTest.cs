using Libplanet.Types;

namespace Libplanet.Net.Tests.Transports;

public sealed class TransportTest(ITestOutputHelper output)
    : TransportTestBase(output)
{
    protected override ITransport CreateTransport(PrivateKey privateKey, TransportOptions transportOptions)
        => new Transport(privateKey.AsSigner(), transportOptions);
}
