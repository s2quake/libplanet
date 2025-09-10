using JSSoft.Commands;
using JSSoft.Commands.Extensions;
using Libplanet.Commands.Extensions;
using Libplanet.KeyStore;
using Libplanet.Types;

namespace Libplanet.Commands.Keys;

[CommandSummary("Create a new private key.")]
[CommandStaticProperty(typeof(PassphraseProperties))]
[CommandStaticProperty(typeof(FormatProperties))]
public sealed class CreateKeyCommand(KeyCommand keyCommand)
    : CommandBase(keyCommand, "create")
{
    [CommandPropertySwitch]
    [CommandSummary("Do not add to the key store, but only show the created key.")]
    public bool DryRun { get; set; }

    [CommandProperty]
    [CommandSummary("Path to key store")]
    [CommandPropertyExclusion(nameof(DryRun))]
    public string Path { get; set; } = string.Empty;

    protected override void OnExecute()
    {
        var passphrase = PassphraseProperties.GetPassphrase();
        var privateKey = new PrivateKey();
        var keyStore = Path == string.Empty ? Web3KeyStore.DefaultKeyStore : new Web3KeyStore(Path);
        var ppk = ProtectedPrivateKey.Protect(privateKey, passphrase);
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
}
