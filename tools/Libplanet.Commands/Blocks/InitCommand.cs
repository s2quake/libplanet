// using System.IO;
// using System.Text.Json;
// using global::Cocona;
// using JSSoft.Commands;
// using Libplanet.Serialization;
// using Libplanet.Types;

// namespace Libplanet.Commands.Blocks;

// public sealed class InitCommand : CommandAsyncBase
// {
//     [Command(Description = "Generate a genesis block.")]
//     public void GenerateGenesis(
//         [Argument(Name = "KEY-ID", Description = "A private key to use for signing.")]
//         Guid keyId,
//         PassphraseParameters passphrase,
//         [Argument(Name = "FILE", Description = "File to write the genesis block to.  " +
//             "Use `-` for stdout (you can still refer to a file named \"-\" by \"./-\").")]
//         string file,
//         [Option('v', Description = "A public key to use for validating.")]
//         string[] validatorKey,
//         BlockPolicyParams blockPolicyParams)
//     {
//         // FIXME: Declare a ICommandParameterSet type taking key ID and keystore path instead:
//         PrivateKey key = new KeyCommand().UnprotectKey(keyId, passphrase, ignoreStdin: true);
//         var signer = key.AsSigner();

//         var validatorSet =
//             validatorKey
//                 .Select(Address.Parse)
//                 .Select(k => new Validator { Address = k })
//                 .ToImmutableSortedSet();
//         // var action = new Initialize { Validators = validatorSet, };
//         var genesis = new GenesisBlockBuilder
//         {
//             Validators = validatorSet,
//             // Transactions =
//             // [
//             //     new TransactionBuilder
//             //     {
//             //         Actions = [action],
//             //     }.Create(signer),
//             // ],
//         }.Create(signer);

//         using Stream stream = file == "-"
//             ? Console.OpenStandardOutput()
//             : File.Open(file, FileMode.Create);
//         switch (format)
//         {
//             // FIXME: Configure JsonConverter for Block:
//             case OutputFormat.Json:
//                 var writerOptions = new JsonWriterOptions { Indented = true };
//                 using (var writer = new Utf8JsonWriter(stream, writerOptions))
//                 {
//                     var serializerOptions = new JsonSerializerOptions
//                     {
//                         AllowTrailingCommas = false,
//                         DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
//                         PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
//                         IgnoreReadOnlyProperties = false,
//                     };
//                     JsonSerializer.Serialize(writer, genesis, serializerOptions);
//                 }

//                 using (var textWriter = new StreamWriter(stream))
//                 {
//                     textWriter.WriteLine();
//                 }

//                 break;

//             default:
//                 ModelSerializer.SerializeToBytes(genesis);
//                 break;
//         }
//     }
// }
