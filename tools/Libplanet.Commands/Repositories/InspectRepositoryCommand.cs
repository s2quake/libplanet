using System.ComponentModel.DataAnnotations;
using System.IO;
using JSSoft.Commands;
using Libplanet.Data;
using Libplanet.Data.LiteDB;
using Libplanet.Data.RocksDB;

namespace Libplanet.Commands.Repositories;

[CommandStaticProperty(typeof(FormatProperties))]
public sealed class InspectRepositoryCommand(RepositoryCommand repositoryCommand)
    : CommandBase(repositoryCommand, "inspect")
{
    [CommandPropertyRequired]
    public string Path { get; set; } = string.Empty;

    [CommandProperty(InitValue = "")]
    [AllowedValues("", "litedb", "rocksdb")]
    public string Type { get; set; } = string.Empty;

    protected override void OnExecute()
    {
        var fullPath = System.IO.Path.GetFullPath(Path);
        var type = Type == string.Empty ? FindType(fullPath) : Type;
        var repository = LoadRepository(fullPath, type);
        var repositoryInfo = new RepositoryInfo(repository);

        FormatProperties.WriteLine(Out, repositoryInfo);
    }

    private static string FindType(string path)
    {
        var typePath = System.IO.Path.Combine(path, "type");
        if (!File.Exists(typePath))
        {
            throw new FileNotFoundException($"The repository type file does not exist: {typePath}");
        }

        return File.ReadAllText(typePath).Trim();
    }

    private Repository LoadRepository(string path, string type)
    {
        return type switch
        {
            "litedb" => new LiteRepository(path),
            "rocksdb" => new RocksRepository(path),
            _ => throw new NotSupportedException($"Unsupported repository type: {Type}"),
        };
    }
}
