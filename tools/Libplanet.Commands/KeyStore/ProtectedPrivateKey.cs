using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Libplanet.KeyStore.Ciphers;
using Libplanet.KeyStore.Kdfs;
using Libplanet.Types;
using Org.BouncyCastle.Crypto.Digests;

namespace Libplanet.KeyStore;

public sealed class ProtectedPrivateKey
{
    public ProtectedPrivateKey(
        Address address,
        IKdf kdf,
        byte[] mac,
        ICipher cipher,
        byte[] ciphertext)
        : this(
            address,
            kdf,
            ImmutableArray.Create(mac),
            cipher,
            ImmutableArray.Create(ciphertext))
    {
    }

    public ProtectedPrivateKey(
        Address address,
        IKdf kdf,
        ImmutableArray<byte> mac,
        ICipher cipher,
        ImmutableArray<byte> ciphertext)
    {
        Address = address;
        Kdf = kdf;
        Mac = mac;
        Cipher = cipher;
        Ciphertext = ciphertext;
    }

    public Address Address { get; }

    public IKdf Kdf { get; }

    public ImmutableArray<byte> Mac { get; }

    public ICipher Cipher { get; }

    public ImmutableArray<byte> Ciphertext { get; }

    public static ProtectedPrivateKey Protect(PrivateKey privateKey, string passphrase)
    {
        var salt = new byte[32];
        using RandomNumberGenerator rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);
        var kdf = new Pbkdf2<Sha256Digest>(10240, salt, 32);
        ImmutableArray<byte> derivedKey = kdf.Derive(passphrase);
        ImmutableArray<byte> encKey = MakeEncryptionKey(derivedKey);
        var iv = new byte[16];
        rng.GetBytes(iv);
        var cipher = new Aes128Ctr(iv);
        ImmutableArray<byte> ciphertext = cipher.Encrypt(encKey, privateKey.Bytes);
        ImmutableArray<byte> mac = CalculateMac(derivedKey, ciphertext);
        Address address = privateKey.Address;
        return new ProtectedPrivateKey(address, kdf, mac, cipher, ciphertext);
    }

    public static ProtectedPrivateKey FromJson(string json)
    {
        var options = new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        };

        using JsonDocument doc = JsonDocument.Parse(json, options);
        JsonElement rootElement = doc.RootElement;
        if (rootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                "The root of the key JSON must be an object, but it is a/an " +
                $"{rootElement.ValueKind}.");
        }

        if (!rootElement.TryGetProperty("version", out JsonElement versionElement))
        {
            throw new InvalidOperationException(
                "The key JSON must contain \"version\" field, but it lacks.");
        }

        if (versionElement.ValueKind != JsonValueKind.Number ||
            !versionElement.TryGetDecimal(out decimal versionNum))
        {
            throw new InvalidOperationException("The \"version\" field must be a number.");
        }
        else if (versionNum != 3)
        {
            throw new InvalidOperationException(
                $"The key JSON format version {versionNum} is unsupported; " +
                "Only version 3 is supported.");
        }

        string GetStringProperty(JsonElement element, string fieldName)
        {
            if (!element.TryGetProperty(fieldName, out JsonElement fieldElement))
            {
                throw new InvalidOperationException(
                    $"The key JSON must contain \"{fieldName}\" field, but it lacks.");
            }

            string str;
            try
            {
                str = fieldElement.GetString();
            }
            catch (InvalidOperationException)
            {
                throw new InvalidOperationException(
                    $"The \"{fieldName}\" field must be a string.");
            }

            if (str is null)
            {
                throw new InvalidOperationException(
                    $"The \"{fieldName}\" field must not be null, but a string.");
            }

            return str;
        }

        JsonElement GetObjectProperty(JsonElement element, string fieldName)
        {
            if (!element.TryGetProperty(fieldName, out var fieldElement))
            {
                throw new InvalidOperationException(
                    $"The key JSON must contain \"{fieldName}\" field, but it lacks.");
            }
            else if (fieldElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    $"The \"{fieldName}\" field must be an object, but it is a/an " +
                    $"{fieldElement.ValueKind}.");
            }

            return fieldElement;
        }

        byte[] GetHexProperty(JsonElement element, string fieldName)
        {
            string str = GetStringProperty(element, fieldName);
            byte[] bytes;
            try
            {
                bytes = ByteUtility.ParseHex(str);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(
                    $"The \"{fieldName}\" field must be a hexadecimal string.\n{e}");
            }

            return bytes;
        }

        JsonElement crypto = GetObjectProperty(rootElement, "crypto");
        string cipherType = GetStringProperty(crypto, "cipher");
        JsonElement cipherParamsElement = GetObjectProperty(crypto, "cipherparams");
        byte[] ciphertext = GetHexProperty(crypto, "ciphertext");
        byte[] mac = GetHexProperty(crypto, "mac");
        string kdfType = GetStringProperty(crypto, "kdf");
        JsonElement kdfParamsElement = GetObjectProperty(crypto, "kdfparams");
        byte[] addressBytes = GetHexProperty(rootElement, "address");
        Address address;
        try
        {
            address = new Address([.. addressBytes]);
        }
        catch (ArgumentException e)
        {
            throw new InvalidOperationException(
                "The \"address\" field must contain an Ethereum-style address which " +
                "consists of 40 hexadecimal letters: " + e);
        }

        var cipher = cipherType switch
        {
            "aes-128-ctr" => Aes128Ctr.FromJson(cipherParamsElement),
            _ =>
                throw new NotSupportedException(
                    $"Unsupported cipher type: \"{cipherType}\"."),
        };

        IKdf kdf;
        try
        {
            kdf = kdfType switch
            {
                "pbkdf2" => Pbkdf2.FromJson(kdfParamsElement),
                "scrypt" => Scrypt.FromJson(kdfParamsElement),
                _ =>
                    throw new NotSupportedException(
                        $"Unsupported cipher type: \"{kdfType}\"."),
            };
        }
        catch (ArgumentException e)
        {
            throw new InvalidOperationException(e.Message);
        }

        return new ProtectedPrivateKey(address, kdf, mac, cipher, ciphertext);
    }

    public PrivateKey Unprotect(string passphrase)
    {
        ImmutableArray<byte> derivedKey = Kdf.Derive(passphrase);
        var mac = CalculateMac(derivedKey, Ciphertext);
        if (!Mac.SequenceEqual(mac))
        {
            throw new InvalidOperationException(
                "The input passphrase is incorrect.");
            // nameof(passphrase),
            // Mac,
            // mac);
        }

        ImmutableArray<byte> encKey = MakeEncryptionKey(derivedKey);
        ImmutableArray<byte> plaintext = Cipher.Decrypt(encKey, Ciphertext);

        var key = new PrivateKey(plaintext.ToBuilder().ToImmutableArray());
        Address actualAddress = key.Address;
        if (!Address.Equals(actualAddress))
        {
            throw new NotSupportedException(
                "The actual address of the unprotected private key does not match to " +
                "the expected address of the protected private key.");
            // Address,
            // actualAddress);
        }

        return key;
    }

    public static ProtectedPrivateKey FromDynamic(dynamic obj)
    {
        if (obj.version is not int version)
        {
            throw new ArgumentException("The \"version\" field must be an integer.", nameof(obj));
        }

        if (version != 3)
        {
            throw new NotSupportedException(
                $"The key JSON format version {obj.version} is unsupported; " +
                "Only version 3 is supported.");
        }

        if (obj.address is not string addressString)
        {
            throw new ArgumentException("The \"address\" field must be a string.", nameof(obj));
        }

        if (obj.crypto.ciphertext is not string ciphertextString)
        {
            throw new ArgumentException("The \"ciphertext\" field must be a string.", nameof(obj));
        }

        if (obj.crypto.mac is not string macString)
        {
            throw new ArgumentException("The \"mac\" field must be a string.", nameof(obj));
        }

        var address = Address.Parse(addressString);
        var mac = ByteUtility.ParseHex(macString);
        var ciphertext = ByteUtility.ParseHex(ciphertextString);

        ICipher cipher = obj.crypto.cipher switch
        {
            "aes-128-ctr" => Aes128Ctr.FromDynamic(obj.crypto.cipherparams),
            _ => throw new NotSupportedException($"Unsupported cipher type: \"{obj.crypto.cipher}\"."),
        };

        IKdf kdf = obj.crypto.kdf switch
        {
            "pbkdf2" => Pbkdf2<Sha256Digest>.FromDynamic(obj.crypto.kdfparams),
            "scrypt" => Scrypt.FromDynamic(obj.crypto.kdfparams),
            _ => throw new NotSupportedException($"Unsupported kdf type: \"{obj.crypto.kdf}\"."),
        };

        return new ProtectedPrivateKey(address, kdf, mac, cipher, ciphertext);
    }

    public dynamic ToDynamic(Guid keyId)
    {
        return new
        {
            version = 3,
            id = keyId,
            address = $"{Address}",
            crypto = new
            {
                ciphertext = ByteUtility.Hex(Ciphertext),
                cipherparams = Cipher.ToDynamic(),
                cipher = Cipher.Name,
                kdfparams = Kdf.ToDynamic(),
                kdf = Kdf.Name,
                mac = ByteUtility.Hex(Mac),
            },
        };
    }

    public void WriteJson(Utf8JsonWriter writer, [Pure] in Guid? id = null)
    {
        writer.WriteStartObject();
        writer.WriteNumber("version", 3);
        writer.WriteString(
            "id",
            (id ?? Guid.NewGuid()).ToString().ToLower(CultureInfo.InvariantCulture));
        writer.WriteString("address", $"{Address:raw}".ToLower(CultureInfo.InvariantCulture));
        writer.WriteStartObject("crypto");
        writer.WriteString("ciphertext", ByteUtility.Hex(Ciphertext));
        writer.WritePropertyName("cipherparams");
        string cipherName = Cipher.WriteJson(writer);
        writer.WriteString("cipher", cipherName);
        writer.WritePropertyName("kdfparams");
        string kdfName = Kdf.WriteJson(writer);
        writer.WriteString("kdf", kdfName);
        writer.WriteString("mac", ByteUtility.Hex(Mac));
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    public void WriteJson(Stream stream, [Pure] in Guid? id = null)
    {
        using var writer = new Utf8JsonWriter(stream);
        WriteJson(writer, id);
    }

    private static ImmutableArray<byte> MakeEncryptionKey(ImmutableArray<byte> derivedKey)
    {
        const int keySubBytes = 16;
        return ImmutableArray.Create(derivedKey, 0, derivedKey.Length - keySubBytes);
    }

    private static ImmutableArray<byte> CalculateMac(
        ImmutableArray<byte> derivedKey,
        ImmutableArray<byte> ciphertext)
    {
        const int keySubBytes = 16;
        var seal = new byte[keySubBytes + ciphertext.Length];
        derivedKey.CopyTo(derivedKey.Length - keySubBytes, seal, 0, keySubBytes);
        ciphertext.CopyTo(seal, keySubBytes);
        var digest = new KeccakDigest(256);
        var mac = new byte[digest.GetDigestSize()];
        digest.BlockUpdate(seal, 0, seal.Length);
        digest.DoFinal(mac, 0);
        return ImmutableArray.Create(mac);
    }
}
