using JSSoft.Commands;

namespace Libplanet.Commands.Keys;

[CommandSummary("Remove a private key.")]
public sealed class RemoveKeyCommand(KeyCommand keyCommand)
    : CommandBase(keyCommand, "rm")
{
    [CommandPropertyRequired]
    public Guid KeyId { get; set; }

    [CommandProperty]
    [CommandSummary("Path to key store")]
    public string StorePath { get; set; } = string.Empty;

    [CommandPropertySwitch("yes", 'y')]
    public bool Yes { get; set; }

    protected override void OnExecute()
    {
        var keyStore = StorePath == string.Empty ? Web3KeyStore.Default : new Web3KeyStore(StorePath);
        if (!keyStore.Contains(KeyId))
        {
            throw new KeyNotFoundException($"The key {KeyId} does not exist.");
        }

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
