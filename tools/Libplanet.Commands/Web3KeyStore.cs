using System.Collections;
using System.IO;
using Libplanet.Types;
using Nethereum.KeyStore;

namespace Libplanet.Commands;

public sealed class Web3KeyStore : IEnumerable<Guid>
{
    private static readonly string DefaultPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) is { } p && p.Length != 0
            ? p
            : System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config"),
        "planetarium",
        "keystore");

    public Web3KeyStore(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        Path = path;
    }

    public Address this[Guid keyId]
    {
        get
        {
            var keyStoreService = new KeyStoreService();
            var keyPath = System.IO.Path.Combine(Path, $"{keyId}");
            var json = File.ReadAllText(keyPath);
            var s = keyStoreService.GetAddressFromKeyStore(json);
            var publicKey = PublicKey.Parse(s);
            return publicKey.Address;
        }
    }

    public static Web3KeyStore Default => new(DefaultPath);

    public string Path { get; }

    public PrivateKey Get(Guid keyId, string passphrase)
    {
        var keyStoreService = new KeyStoreService();
        var keyPath = System.IO.Path.Combine(Path, $"{keyId}");
        var json = File.ReadAllText(keyPath);
        var bytes = keyStoreService.DecryptKeyStoreFromJson(passphrase, json);
        return new(bytes);
    }

    public string GetJson(Guid keyId)
    {
        var keyPath = System.IO.Path.Combine(Path, $"{keyId}");
        return File.ReadAllText(keyPath);
    }

    public bool Contains(Guid keyId)
    {
        foreach (var (id, _) in ListFiles())
        {
            if (id.Equals(keyId))
            {
                return true;
            }
        }

        return false;
    }

    public Guid Add(PrivateKey privateKey, string passphrase)
    {
        var keyId = Guid.NewGuid();
        var keyPath = System.IO.Path.Combine(Path, $"{keyId}");
        var keyStoreService = new KeyStoreService();
        var json = keyStoreService.EncryptAndGenerateDefaultKeyStoreAsJson(
            passphrase, [.. privateKey.Bytes], $"{privateKey.PublicKey}");
        File.WriteAllText(keyPath, json);
        return keyId;
    }

    public Guid Add(string json)
    {
        var keyStoreService = new KeyStoreService();
        _ = keyStoreService.GetAddressFromKeyStore(json);
        var keyId = Guid.NewGuid();
        var keyPath = System.IO.Path.Combine(Path, $"{keyId}");
        File.WriteAllText(keyPath, json);
        return keyId;
    }

    public void Remove(Guid id)
    {
        foreach (var (keyId, keyPath) in ListFiles())
        {
            if (keyId.Equals(id))
            {
                File.Delete(keyPath);
                return;
            }
        }

        throw new InvalidOperationException("No key have such ID");
    }

    public IEnumerator<Guid> GetEnumerator()
    {
        foreach (var (keyId, _) in ListFiles())
        {
            yield return keyId;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private IEnumerable<(Guid, string)> ListFiles()
    {
        var keyPaths = Directory.EnumerateFiles(Path);
        foreach (string keyPath in keyPaths)
        {
            if (System.IO.Path.GetFileName(keyPath) is string filename
                && Guid.TryParse(filename, out var keyId))
            {
                yield return (keyId, keyPath);
            }
        }
    }
}
