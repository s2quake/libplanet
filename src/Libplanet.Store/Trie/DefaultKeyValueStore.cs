using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zio;
using Zio.FileSystems;

namespace Libplanet.Store.Trie;

public sealed class DefaultKeyValueStore(string path) : IKeyValueStore
{
    private readonly SubFileSystem _fs = CreateFileSystem(path);
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
            var path = DataPath(key);
            if (!_fs.FileExists(path))
            {
                throw new KeyNotFoundException($"No such key: {key}.");
            }

            return _fs.ReadAllBytes(path);
        }

        set => _fs.WriteAllBytes(DataPath(key), value);
    }

    public bool Remove(in KeyBytes key)
    {
        var path = DataPath(key);
        if (_fs.FileExists(path))
        {
            _fs.DeleteFile(path);
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
            throw new ArgumentException(
                $"The path '{path}' is not fully qualified.",
                nameof(path));
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
