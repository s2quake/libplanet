using JSSoft.Commands;
using Libplanet.KeyStore;

namespace Libplanet.Commands.Keys;

[CommandSummary("Remove a private key.")]
public sealed class RemoveKeyCommand(KeyCommand keyCommand)
    : CommandBase(keyCommand, "rm")
{
    [CommandPropertyRequired]
    public Guid KeyId { get; set; }

    [CommandProperty]
    [CommandSummary("Path to key store")]
    public string Path { get; set; } = string.Empty;

    [CommandPropertySwitch("yes", 'y')]
    public bool Yes { get; set; }

    protected override void OnExecute()
    {
        var keyStore = Path == string.Empty ? Web3KeyStore.DefaultKeyStore : new Web3KeyStore(Path);
        _ = keyStore.Get(KeyId);
        if (Yes || ConsoleConfirmationReader.Read($"Are you sure to remove the key {KeyId})?"))
        {
            keyStore.Remove(KeyId);
            Error.WriteLine($"The key {KeyId} has been removed.");
        }
        else
        {
            throw new InvalidOperationException("Removal cancelled.");
        }
    }
}
