using JSSoft.Commands;
using JSSoft.Commands.Extensions;
using Libplanet.Commands.Extensions;
using Libplanet.KeyStore;
using Libplanet.Types;

namespace Libplanet.Commands.Keys;

[CommandSummary("Import a raw private key or Web3 Secret Storage.")]
[CommandStaticProperty(typeof(PassphraseProperties))]
[CommandStaticProperty(typeof(FormatProperties))]
public sealed class ImportKeyCommand(KeyCommand keyCommand)
    : CommandBase(keyCommand, "import")
{
    [CommandPropertyRequired]
    [CommandSummary("A raw private key in hexadecimal string, or path to Web3 Secret Storage to import.")]
    public string Key { get; set; } = string.Empty;

    [CommandPropertySwitch("key-json")]
    [CommandSummary("Use the given key value as Web3 Secret Storage Formatted json.")]
    [CommandPropertyExclusion(nameof(PassphraseProperties.Passphrase))]
    [CommandPropertyExclusion(nameof(PassphraseProperties.PassphraseFile))]
    public bool KeyAsJson { get; set; }

    [CommandPropertySwitch]
    [CommandSummary("Do not add to the key store, but only show the created key.")]
    public bool DryRun { get; set; }

    [CommandProperty]
    [CommandSummary("Path to key store")]
    [CommandPropertyExclusion(nameof(DryRun))]
    public string Path { get; set; } = string.Empty;

    protected override void OnExecute()
    {
        var keyStore = Path == string.Empty ? Web3KeyStore.DefaultKeyStore : new Web3KeyStore(Path);
        var ppk = GetProtectedPrivateKey();
        var keyId = DryRun ? Guid.NewGuid() : keyStore.Add(ppk);

        if (FormatProperties.Json)
        {
            Out.WriteLineAsJson((object)ppk.ToDynamic(keyId));
        }
        else
        {
            var tableDataBuilder = new TableDataBuilder(["Key ID", "Address"]);
            tableDataBuilder.Add([keyId.ToString(), $"{ppk.Address}"]);
            Out.Print(tableDataBuilder);
        }
    }

    private ProtectedPrivateKey GetProtectedPrivateKey()
    {
        if (KeyAsJson)
        {
            dynamic obj = JsonUtility.Deserialize(Key);
            return ProtectedPrivateKey.FromDynamic(obj);
        }

        var privateKey = PrivateKey.Parse(Key);
        var passphrase = PassphraseProperties.GetPassphrase();
        return ProtectedPrivateKey.Protect(privateKey, passphrase);
    }
}
