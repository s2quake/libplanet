using System.Diagnostics.CodeAnalysis;
using System.IO;
using Zio;
using Zio.FileSystems;

namespace Libplanet.Store.Trie;

public sealed class DefaultTable(string path) : KeyValueStoreBase, IDisposable
{
    private readonly FileSystem _fs = path == string.Empty ? new MemoryFileSystem() : CreateFileSystem(path);
    private bool _isDisposed;
    private int? _count;

    public DefaultTable()
        : this(string.Empty)
    {
    }

    public string Path => path;

    public override int Count => _count ??= _fs.EnumerateFiles(UPath.Root).Count();

    public override byte[] this[KeyBytes key]
    {
        get
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            var dataPath = DataPath(key);
            if (!_fs.FileExists(dataPath))
            {
                throw new KeyNotFoundException($"No such key: {key}.");
            }

            return _fs.ReadAllBytes(dataPath);
        }

        set
        {
            var dataPath = DataPath(key);
            var exists = _fs.FileExists(dataPath);
            _fs.WriteAllBytes(DataPath(key), value);
            if (!exists && _count is not null)
            {
                _count++;
            }
        }
    }

    public override bool Remove(KeyBytes key)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        var dataPath = DataPath(key);
        if (_fs.FileExists(dataPath))
        {
            _fs.DeleteFile(dataPath);
            if (_count is not null)
            {
                _count--;
            }

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
            if (System.IO.Path.IsPathFullyQualified(path))
            {
                Directory.Delete(path, recursive: true);
            }

            GC.SuppressFinalize(this);
        }
    }

    public override void Add(KeyBytes key, byte[] value)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        var dataPath = DataPath(key);
        if (_fs.FileExists(dataPath))
        {
            throw new ArgumentException($"Key {key} already exists", nameof(key));
        }

        _fs.WriteAllBytes(dataPath, value);
        if (_count is not null)
        {
            _count++;
        }
    }

    public override bool ContainsKey(KeyBytes key)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        return _fs.FileExists(DataPath(key));
    }

    public override bool TryGetValue(KeyBytes key, [MaybeNullWhen(false)] out byte[] value)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
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
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        foreach (var file in _fs.EnumerateFiles(UPath.Root))
        {
            _fs.DeleteFile(file);
        }

        _count = 0;
    }

    protected override IEnumerable<KeyBytes> EnumerateKeys()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        foreach (var item in _fs.EnumerateFiles(UPath.Root))
        {
            yield return KeyBytes.Parse(item.GetName());
        }
    }

    private static SubFileSystem CreateFileSystem(string path)
    {
        if (!System.IO.Path.IsPathFullyQualified(path))
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
