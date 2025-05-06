using System.IO;
using Zio;
using Zio.FileSystems;

namespace Libplanet.Store.Trie;

public sealed class DefaultKeyValueStore(string path) : IKeyValueStore
{
    private readonly FileSystem _fs = path == string.Empty ? new MemoryFileSystem() : CreateFileSystem(path);
    private bool _isDisposed;

    public DefaultKeyValueStore()
        : this(string.Empty)
    {
    }

    public IEnumerable<KeyBytes> Keys =>
        _fs.EnumerateFiles(UPath.Root).Select(path => KeyBytes.Parse(path.GetName()));

    public byte[] this[in KeyBytes key]
    {
        get
        {
            var dataPath = DataPath(key);
            if (!_fs.FileExists(dataPath))
            {
                throw new KeyNotFoundException($"No such key: {key}.");
            }

            return _fs.ReadAllBytes(dataPath);
        }

        set => _fs.WriteAllBytes(DataPath(key), value);
    }

    public bool Remove(in KeyBytes key)
    {
        var dataPath = DataPath(key);
        if (_fs.FileExists(dataPath))
        {
            _fs.DeleteFile(dataPath);
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _fs.Dispose();
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public bool ContainsKey(in KeyBytes key) => _fs.FileExists(DataPath(key));

    private static SubFileSystem CreateFileSystem(string path)
    {
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException($"The path '{path}' is not fully qualified.", nameof(path));
        }

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        var pfs = new PhysicalFileSystem();
        return new SubFileSystem(pfs, pfs.ConvertPathFromInternal(path), owned: true);
    }

    private static UPath DataPath(in KeyBytes key) => UPath.Root / $"{key:h}";
}
