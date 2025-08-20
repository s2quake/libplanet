using Libplanet.Types;

namespace Libplanet.Net.Tests.Transports;

public sealed class TransportTest(ITestOutputHelper output)
    : TransportTestBase(output)
{
    protected override ITransport CreateTransport(ISigner signer, TransportOptions transportOptions)
        => new Transport(signer, transportOptions);
}
