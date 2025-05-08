using System.IO;
using Libplanet.Store.Trie;

namespace Libplanet.Store;

public sealed class DefaultDatabase : Database<DefaultKeyValueStore>
{
    private readonly string _path;

    public DefaultDatabase(string path)
    {
        if (path != string.Empty)
        {
            if (!Path.IsPathFullyQualified(path))
            {
                throw new ArgumentException(
                    "The path must be fully qualified.", nameof(path));
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        _path = path;
    }

    protected override DefaultKeyValueStore Create(string key)
        => new(_path == string.Empty ? string.Empty : Path.Combine(_path, key));
}
