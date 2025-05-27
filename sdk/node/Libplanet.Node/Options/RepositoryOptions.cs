using System.ComponentModel;

namespace Libplanet.Node.Options;

[Options(Position)]
public sealed class RepositoryOptions : OptionsBase<RepositoryOptions>
{
    public const string Position = "Store";

    public const string DefaultRootPath = ".db";
    public const string DefaultStorePath = "store";
    public const string DefaultStateStorePath = "state";

    /// <summary>
    /// The type of the store.
    /// </summary>
    [Description("The type of the store.")]
    public RepositoryType Type { get; set; } = RepositoryType.InMemory;

    /// <summary>
    /// The root directory path of the store.
    /// </summary>
    [Description("The root directory path of the store.")]
    public string Path { get; set; } = string.Empty;
}
