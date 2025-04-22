namespace Libplanet.Crypto;

public static class CryptoConfig
{
    private static ICryptoBackend? _cryptoBackend;

    public static ICryptoBackend CryptoBackend
    {
        get => _cryptoBackend ??= new DefaultCryptoBackend();
        set => _cryptoBackend = value;
    }
}
