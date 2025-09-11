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
    public string StorePath { get; set; } = string.Empty;

    protected override void OnExecute()
    {
        var keyStore = StorePath == string.Empty ? Web3KeyStore.DefaultKeyStore : new Web3KeyStore(StorePath);
        var rows = new Dictionary<string, string>();
        foreach (var item in keyStore.List())
        {
            rows[item.Item1.ToString()] = item.Item2.Address.ToString();
        }

        FormatProperties.WriteLine(Out, rows);

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
