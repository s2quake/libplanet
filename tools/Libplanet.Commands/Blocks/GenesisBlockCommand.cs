using System.IO;
using System.Security.Cryptography;
using JSSoft.Commands;
using Libplanet.Commands.Extensions;
using Libplanet.KeyStore;
using Libplanet.Serialization;
using Libplanet.Serialization.Json;
using Libplanet.Serialization.Yaml;
using Libplanet.Types;

namespace Libplanet.Commands.Blocks;

[CommandSummary("Generate a genesis block.")]
[CommandStaticProperty(typeof(PassphraseProperties))]
[CommandStaticProperty(typeof(FormatProperties))]
public sealed class GenesisBlockCommand(BlockCommand blockCommand)
    : CommandBase(blockCommand, "genesis")
{
    [CommandPropertyRequired]
    [CommandSummary("A key UUID to export.")]
    public string KeyId { get; set; } = string.Empty;

    [CommandProperty]
    [CommandSummary("A list of validator addresses. (e.g. 'address1,address2:power2')")]
    public string[] Validators { get; set; } = [];

    [CommandProperty]
    public string OutputPath { get; set; } = string.Empty;

    [CommandPropertySwitch]
    public bool Force { get; set; }

    [CommandProperty]
    [CommandSummary("Path to key store")]
    public string StorePath { get; set; } = string.Empty;

    [CommandProperty]
    public string StateRootHash { get; set; } = string.Empty;

    [CommandProperty]
    public int Height { get; set; }

    [CommandProperty]
    public DateTimeOffset? Timestamp { get; set; }

    [CommandProperty]
    public string States { get; set; } = string.Empty;

    protected override void OnExecute()
    {
        // if (OutputPath == string.Empty)
        // {
        //     throw new InvalidOperationException("Output path is not set.");
        // }

        // if (File.Exists(OutputPath) && !Force)
        // {
        //     throw new InvalidOperationException($"File already exists: {OutputPath}");
        // }

        var keyId = Guid.Parse(KeyId);
        var keyStore = StorePath == string.Empty ? Web3KeyStore.DefaultKeyStore : new Web3KeyStore(StorePath);
        var ppk = keyStore.Get(keyId);
        var passphrase = PassphraseProperties.GetPassphrase(keyId);
        var privateKey = ppk.Unprotect(passphrase);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = GetValidators(Validators, privateKey.Address),
            StateRootHash = StateRootHash == string.Empty ? default : HashDigest<SHA256>.Parse(StateRootHash),
            Height = Height,
            Timestamp = Timestamp ?? DateTimeOffset.UtcNow,
        }.Create(privateKey.AsSigner());

        // var bytes = ModelSerializer.Serialize(genesisBlock);
        // File.WriteAllBytes(OutputPath, bytes);
        var data = ModelJsonSerializer.Serialize(genesisBlock);
        Out.Write(data);
        // Out.WriteLineAsJson(genesisBlock);
    }

    private static ImmutableSortedSet<Validator> GetValidators(string[] validators, Address proposer)
    {
        if (validators.Length == 0)
        {
            return [new Validator { Address = proposer, Power = 1 }];
        }

        var validatorList = new List<Validator>(validators.Length);
        foreach (var validator in validators)
        {
            validatorList.Add(GetValidator(validator));
        }

        return [.. validatorList];
    }

    private static Validator GetValidator(string validator)
    {
        var parts = validator.Split(':');
        if (parts.Length is 1)
        {
            return new Validator { Address = Address.Parse(parts[0]) };
        }
        else if (parts.Length is 2)
        {
            return new Validator { Address = Address.Parse(parts[0]), Power = BigInteger.Parse(parts[1]) };
        }

        throw new ArgumentException($"Invalid validator format: {validator}", nameof(validator));
    }
}
