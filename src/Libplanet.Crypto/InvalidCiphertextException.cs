namespace Libplanet.Crypto;

public sealed class InvalidCiphertextException : Exception
{
    public InvalidCiphertextException()
    {
    }

    public InvalidCiphertextException(string message)
        : base(message)
    {
    }

    public InvalidCiphertextException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
