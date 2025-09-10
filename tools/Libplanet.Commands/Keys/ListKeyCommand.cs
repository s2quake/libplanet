using JSSoft.Commands;
using Libplanet.Commands.Extensions;
using Libplanet.KeyStore;

namespace Libplanet.Commands.Keys;

[CommandSummary("List all private keys.")]
[CommandStaticProperty(typeof(FormatProperties))]
public sealed class ListKeyCommand(KeyCommand keyCommand)
    : CommandBase(keyCommand, "list")
{
    [CommandProperty]
    [CommandSummary("Specify key store path to list.")]
    public string Path { get; set; } = string.Empty;

    protected override void OnExecute()
    {
        var keyStore = Path == string.Empty ? Web3KeyStore.DefaultKeyStore : new Web3KeyStore(Path);
        var rows = new SortedDictionary<string, string>();
        foreach (var item in keyStore.List())
        {
            rows[item.Item1.ToString()] = item.Item2.Address.ToString();
        }

        if (FormatProperties.Json)
        {
            Out.WriteLineAsJson(rows);
        }
        else
        {
            Out.WriteLineAsTable(rows, header: ("Key ID", "Address"));
        }
    }
}
