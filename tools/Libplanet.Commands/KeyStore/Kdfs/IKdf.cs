using System.Text.Json;

namespace Libplanet.KeyStore.Kdfs;

/// <summary>
/// An interface to form key derivation functions (KDF) that are used to derive a valid
/// cryptographic key from a user input passphrase (i.e., password).
/// </summary>
public interface IKdf
{
    string Name{ get; }

    ImmutableArray<byte> Derive(string passphrase);

    string WriteJson(Utf8JsonWriter writer);

    dynamic ToDynamic();
}
