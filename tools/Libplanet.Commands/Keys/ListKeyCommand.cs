using JSSoft.Commands;

namespace Libplanet.Commands.Keys;

[CommandSummary("List all keys and their addresses.")]
[CommandStaticProperty(typeof(FormatProperties))]
public sealed class ListKeyCommand(KeyCommand keyCommand)
    : CommandBase(keyCommand, "list")
{
    [CommandProperty]
    [CommandSummary("Specify key store path to list.")]
    public string StorePath { get; set; } = string.Empty;

    protected override void OnExecute()
    {
        var keyStore = StorePath == string.Empty ? Web3KeyStore.Default : new Web3KeyStore(StorePath);
        var keyInfoList = new List<KeyInfo>();
        foreach (var keyId in keyStore)
        {
            keyInfoList.Add(new KeyInfo
            {
                KeyId = keyId.ToString(),
                Address = keyStore[keyId].ToString(),
            });
        }

        FormatProperties.WriteLine(Out, keyInfoList);
    }
}
