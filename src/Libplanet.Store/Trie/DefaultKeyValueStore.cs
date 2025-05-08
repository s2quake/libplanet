using System.Diagnostics.CodeAnalysis;
using System.IO;
using Zio;
using Zio.FileSystems;

namespace Libplanet.Store.Trie;

public sealed class DefaultKeyValueStore(string path) : KeyValueStoreBase, IDisposable
{
    private readonly FileSystem _fs = path == string.Empty ? new MemoryFileSystem() : CreateFileSystem(path);
    private bool _isDisposed;

    public DefaultKeyValueStore()
        : this(string.Empty)
    {
    }

    public override byte[] this[KeyBytes key]
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

    public override bool Remove(KeyBytes key)
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

    public override void Add(KeyBytes key, byte[] value)
    {
        var dataPath = DataPath(key);
        if (_fs.FileExists(dataPath))
        {
            throw new ArgumentException($"Key {key} already exists", nameof(key));
        }

        _fs.WriteAllBytes(dataPath, value);
    }

    public override bool ContainsKey(KeyBytes key) => _fs.FileExists(DataPath(key));

    public override bool TryGetValue(KeyBytes key, [MaybeNullWhen(false)] out byte[] value)
    {
        var dataPath = DataPath(key);
        if (_fs.FileExists(dataPath))
        {
            value = _fs.ReadAllBytes(dataPath);
            return true;
        }

        value = null;
        return false;
    }

    public override void Clear()
    {
        foreach (var file in _fs.EnumerateFiles(UPath.Root))
        {
            _fs.DeleteFile(file);
        }
    }

    protected override IEnumerable<KeyBytes> EnumerateKeys()
    {
        foreach (var item in _fs.EnumerateFiles(UPath.Root))
        {
            yield return KeyBytes.Parse(item.GetName());
        }
    }

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
