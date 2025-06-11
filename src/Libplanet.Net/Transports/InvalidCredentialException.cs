using Libplanet.Types;

namespace Libplanet.Net.Transports;

public class InvalidCredentialException(string message, Address expected, Address actual)
    : Exception(message)
{
    public Address Expected { get; } = expected;

    public Address Actual { get; } = actual;
}
