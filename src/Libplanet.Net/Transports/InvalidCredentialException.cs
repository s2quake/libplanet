using Libplanet.Types;

namespace Libplanet.Net.Transports;

public class InvalidCredentialException : Exception
{
    internal InvalidCredentialException(string message, PublicKey expected, PublicKey actual)
        : base(message)
    {
        Expected = expected;
        Actual = actual;
    }

    public PublicKey Expected { get; }

    public PublicKey Actual { get; }
}
