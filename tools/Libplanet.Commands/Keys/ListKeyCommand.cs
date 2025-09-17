using JSSoft.Commands;
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
        var keyInfoList = new List<KeyInfo>();
        foreach (var item in keyStore.List())
        {
            keyInfoList.Add(new KeyInfo
            {
                KeyId = item.Item1.ToString(),
                PrivateKey = string.Empty,
                Address = item.Item2.Address.ToString(),
                PublicKey = string.Empty,
            });
        }

        FormatProperties.WriteLine(Out, keyInfoList);
    }
}
