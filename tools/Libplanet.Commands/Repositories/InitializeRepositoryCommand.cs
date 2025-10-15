using System.ComponentModel.DataAnnotations;
using System.IO;
using JSSoft.Commands;
using Libplanet.Commands.IO;
using Libplanet.Data;
using Libplanet.Data.LiteDB;
using Libplanet.Data.RocksDB;
using Libplanet.Serialization;
using Libplanet.Serialization.Json;
using Libplanet.Serialization.Yaml;
using Libplanet.Types;

namespace Libplanet.Commands.Repositories;

public sealed class InitializeRepositoryCommand(RepositoryCommand repositoryCommand)
    : CommandBase(repositoryCommand, "init")
{
    [CommandPropertyRequired]
    public string Path { get; set; } = string.Empty;

    [CommandPropertySwitch("force", 'f')]
    public bool Force { get; set; }

    [CommandProperty(InitValue = "litedb")]
    [AllowedValues("litedb", "rocksdb")]
    public string Type { get; set; } = string.Empty;

    [CommandProperty("genesis")]
    public string GenesisBlockPath { get; set; } = string.Empty;

    [CommandProperty("genesis-format", InitValue = "bin")]
    [AllowedValues("bin", "json", "yaml")]
    public string GenesisBlockFormat { get; set; } = string.Empty;

    protected override void OnExecute()
    {
        var fullPath = System.IO.Path.GetFullPath(Path);
        if (!DirectoryUtility.IsNullOrEmpty(fullPath) && !Force)
        {
            throw new IOException(
                $"Directory '{fullPath}' exists or is not empty. Use --force to overwrite.");
        }

        DirectoryUtility.DeleteIfExists(fullPath);
        DirectoryUtility.EnsureDirectory(fullPath);

        var repository = CreateRepository(fullPath);
        if (GenesisBlockPath != string.Empty)
        {
            var genesisBlock = LoadGenesisBlock();
            repository.Append(genesisBlock, default);
        }

        File.WriteAllText(System.IO.Path.Combine(fullPath, "type"), Type);
        Console.WriteLine($"Created a {Type} repository at '{fullPath}'.");
    }

    private Repository CreateRepository(string path)
    {
        return Type.ToLowerInvariant() switch
        {
            "litedb" => new LiteRepository(path),
            "rocksdb" => new RocksRepository(path),
            _ => throw new NotSupportedException($"Unsupported repository type: {Type}"),
        };
    }

    private Block LoadGenesisBlock()
    {
        if (GenesisBlockPath == string.Empty)
        {
            throw new InvalidOperationException("Genesis block path is not specified.");
        }

        if (!File.Exists(GenesisBlockPath))
        {
            throw new FileNotFoundException(
                $"Genesis block file '{GenesisBlockPath}' does not exist.");
        }

        return GenesisBlockFormat switch
        {
            "bin" => ModelSerializer.Deserialize<Block>(File.ReadAllBytes(GenesisBlockPath)),
            "json" => ModelJsonSerializer.Deserialize<Block>(File.ReadAllText(GenesisBlockPath)),
            "yaml" => ModelYamlSerializer.Deserialize<Block>(File.ReadAllText(GenesisBlockPath)),
            _ => throw new NotSupportedException(
                $"Unsupported genesis block format: {GenesisBlockFormat}"),
        };
    }
}
