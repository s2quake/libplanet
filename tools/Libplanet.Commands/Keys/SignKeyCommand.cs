using System.Text;
using JSSoft.Commands;
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
        var keyStore = StorePath == string.Empty ? Web3KeyStore.Default : new Web3KeyStore(StorePath);
        if (!keyStore.Contains(keyId))
        {
            throw new KeyNotFoundException($"The key {KeyId} does not exist.");
        }

        var privateKey = keyStore.Get(keyId, PassphraseProperties.GetPassphrase(keyId));
        var message = MessageAsHex ? ByteUtility.ParseHex(Message) : Encoding.UTF8.GetBytes(Message);
        var bytes = privateKey.Sign(message);

        FormatProperties.WriteLine(Out, ByteUtility.Hex(bytes));
    }
}
