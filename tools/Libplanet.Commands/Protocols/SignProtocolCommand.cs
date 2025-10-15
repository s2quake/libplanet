using JSSoft.Commands;
using Libplanet.Net;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types;

namespace Libplanet.Commands.Protocols;

[CommandSummary("Signs a protocol metadata with a private key.")]
[CommandStaticProperty(typeof(PassphraseProperties))]
public sealed class SignProtocolCommand(ProtocolCommand protocolCommand)
    : CommandBase(protocolCommand, "sign")
{
    [CommandPropertyRequired]
    [CommandSummary("A key UUID to export.")]
    public string KeyId { get; set; } = string.Empty;

    [CommandPropertyRequired]
    [NonNegative]
    public int Version { get; set; }

    [CommandProperty]
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

        var passphrase = PassphraseProperties.GetPassphrase(keyId);
        var privateKey = keyStore.Get(keyId, passphrase);
        var protocol = new ProtocolMetadata
        {
            Version = Version,
            Signer = privateKey.Address,
        }.Sign(privateKey.AsSigner());

        Out.WriteLine(ByteUtility.Hex(ModelSerializer.Serialize(protocol)));
    }
}
