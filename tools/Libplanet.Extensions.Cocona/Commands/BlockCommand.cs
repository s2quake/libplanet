using System.IO;
using System.Text.Json;
using global::Cocona;
using Libplanet.Action;
using Libplanet.Action.Builtin;
using Libplanet.Blockchain;
using Libplanet.Serialization;
using Libplanet.Data;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;
using Libplanet.Types.Tx;

namespace Libplanet.Extensions.Cocona.Commands;

public class BlockCommand
{
    public enum OutputFormat
    {
        /// <summary>
        /// Bencode Extensible Binary Object Notation.
        /// </summary>
        Bencodex,

        /// <summary>
        /// Json (Human-readable).
        /// </summary>
        Json,
    }

    [Command(Description = "Analyze the given block.")]
    public void Analyze(
        [Argument(
            Description = "The file path of the block to analyze.  If " +
                "a hyphen (-) is given it reads from the standard input (if you want to read " +
                "just a file named \"-\", use \"./-\" instead).")]
        string file,
        [Option(
            'o',
            Description = "The file path to write the analysis result to.  " +
                "If a hyphen (-) is given it writes to the standard output (if you want to write " +
                "just a file named \"-\", use \"./-\" instead).  If not given, it writes to " +
                "the standard output.")]
        string output = "-")
    {
        using Stream inputStream = file == "-"
            ? Console.OpenStandardInput()
            : File.OpenRead(file);
        string sourceName = file == "-" ? "stdin" : $"file {file}";
        throw new NotImplementedException();
        // Block block;
        // try
        // {
        //     throw new NotImplementedException();
        //     // var dict = codec.Decode(inputStream);
        //     // block = ModelSerializer.Deserialize<Block>(dict);
        // }
        // catch (DecodingException e)
        // {
        //     throw new CommandExitedException(
        //         $"The {sourceName} does not contain a valid Bencodex data: {e.Message}",
        //         -1);
        // }
        // catch (InvalidCastException)
        // {
        //     throw new CommandExitedException(
        //         $"The {sourceName} does not contain a valid Bencodex dictionary.",
        //         -2);
        // }
        // catch (InvalidOperationException e)
        // {
        //     throw new CommandExitedException(
        //         $"The {sourceName} does not contain a valid block: {e.Message}",
        //         -4);
        // }
        // catch (Exception e) when (e is IndexOutOfRangeException or KeyNotFoundException)
        // {
        //     throw new CommandExitedException(
        //         $"The {sourceName} lacks some required fields.",
        //         -3);
        // }

        // using Stream outputStream = output == "-"
        //     ? Console.OpenStandardOutput()
        //     : File.Open(file, FileMode.Create);
        // var writerOptions = new JsonWriterOptions { Indented = true };
        // using (var writer = new Utf8JsonWriter(outputStream, writerOptions))
        // {
        //     var serializerOptions = new JsonSerializerOptions
        //     {
        //         AllowTrailingCommas = false,
        //         DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        //         PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        //         IgnoreReadOnlyProperties = false,
        //     };
        //     JsonSerializer.Serialize(writer, block, serializerOptions);
        // }

        // using var textWriter = new StreamWriter(outputStream);
        // textWriter.WriteLine();
    }

    [Command(Description = "Generate a genesis block.")]
    public void GenerateGenesis(
        [Argument(Name = "KEY-ID", Description = "A private key to use for signing.")]
        Guid keyId,
        PassphraseParameters passphrase,
        [Argument(Name = "FILE", Description = "File to write the genesis block to.  " +
            "Use `-` for stdout (you can still refer to a file named \"-\" by \"./-\").")]
        string file,
        [Option('v', Description = "A public key to use for validating.")]
        string[] validatorKey,
        BlockPolicyParams blockPolicyParams,
        [Option('f', Description = "Output format.")]
        OutputFormat format = OutputFormat.Bencodex)
    {
        // FIXME: Declare a ICommandParameterSet type taking key ID and keystore path instead:
        PrivateKey key = new KeyCommand().UnprotectKey(keyId, passphrase, ignoreStdin: true);

        var validatorSet =
            validatorKey
                .Select(Address.Parse)
                .Select(k => new Validator { Address = k })
                .ToImmutableSortedSet();
        ImmutableList<Transaction> txs = Array.Empty<Transaction>()

            // FIXME: Remove this pragma after fixing the following issue:
            // https://github.com/dotnet/platform-compat/blob/master/docs/PC002.md
 #pragma warning disable PC002
            .Append(new TransactionMetadata
                {
    #pragma warning restore PC002
                    Nonce = 0,
                    Signer = key.Address,
                    GenesisHash = default,
                    Actions = new IAction[]
                    {
                        new Initialize
                        {
                            Validators = validatorSet,
                        },
                    }.ToBytecodes(),
                }.Sign(key))
            .ToImmutableList();

        var policyActions = blockPolicyParams.GetPolicyActions();
        var actionEvaluator = new ActionEvaluator(
            new StateStore(new DefaultTable()),
            policyActions);
        Block genesis = BlockChain.ProposeGenesisBlock(
            proposer: key, transactions: [.. txs]);
        using Stream stream = file == "-"
            ? Console.OpenStandardOutput()
            : File.Open(file, FileMode.Create);
        switch (format)
        {
            // FIXME: Configure JsonConverter for Block:
            case OutputFormat.Json:
                var writerOptions = new JsonWriterOptions { Indented = true };
                using (var writer = new Utf8JsonWriter(stream, writerOptions))
                {
                    var serializerOptions = new JsonSerializerOptions
                    {
                        AllowTrailingCommas = false,
                        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        IgnoreReadOnlyProperties = false,
                    };
                    JsonSerializer.Serialize(writer, genesis, serializerOptions);
                }

                using (var textWriter = new StreamWriter(stream))
                {
                    textWriter.WriteLine();
                }

                break;

            default:
                ModelSerializer.SerializeToBytes(genesis);
                break;
        }
    }
}
