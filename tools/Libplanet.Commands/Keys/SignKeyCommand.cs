using System.Text;
using JSSoft.Commands;
using Libplanet.Commands.Extensions;
using Libplanet.KeyStore;
using Libplanet.Types;

namespace Libplanet.Commands.Keys;

[CommandSummary("Sign a message.")]
[CommandStaticProperty(typeof(PassphraseProperties))]
[CommandStaticProperty(typeof(FormatProperties))]
public sealed class SignKeyCommand(KeyCommand keyCommand)
    : CommandBase(keyCommand, "sign")
{
    [CommandPropertyRequired]
    [CommandSummary("A key UUID to export.")]
    public string KeyId { get; set; } = string.Empty;

    [CommandPropertyRequired]
    [CommandSummary("Message to sign.")]
    public string Message { get; set; } = string.Empty;

    [CommandPropertySwitch("hex")]
    [CommandSummary("Indicates that the message is in hex format.")]
    public bool MessageAsHex { get; set; }

    [CommandSummary("Path to key store")]
    public string StorePath { get; set; } = string.Empty;

    protected override void OnExecute()
    {
        var keyId = Guid.Parse(KeyId);
        var keyStore = StorePath == string.Empty ? Web3KeyStore.DefaultKeyStore : new Web3KeyStore(StorePath);
        var ppk = keyStore.Get(keyId);
        var passphrase = PassphraseProperties.GetPassphrase(keyId);
        var privateKey = ppk.Unprotect(passphrase);

        var message = MessageAsHex ? ByteUtility.ParseHex(Message) : Encoding.UTF8.GetBytes(Message);
        var bytes = privateKey.Sign(message);

        if (FormatProperties.Json)
        {
            Out.WriteLineAsJson(new { signature = ByteUtility.Hex(bytes) });
        }
        else
        {
            Out.WriteLine(ByteUtility.Hex(bytes));
        }
    }
}
