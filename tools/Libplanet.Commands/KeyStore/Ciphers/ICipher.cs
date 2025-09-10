using System.Text.Json;

namespace Libplanet.KeyStore.Ciphers;

public interface ICipher
{
    string Name { get; }

    ImmutableArray<byte> Encrypt(in ImmutableArray<byte> key, in ImmutableArray<byte> plaintext);

    ImmutableArray<byte> Decrypt(in ImmutableArray<byte> key, in ImmutableArray<byte> ciphertext);

    string WriteJson(Utf8JsonWriter writer);

    dynamic ToDynamic();
}
