using System.IO;

namespace Libplanet.Commands.IO;

public static class DirectoryUtility
{
    public static string EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return Directory.CreateDirectory(path).FullName;
        }

        return Path.GetFullPath(path);
    }

    public static void DeleteIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path);
        }
    }

    public static bool TryDelete(string path, bool recursive)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive);
                return true;
            }
        }
        catch
        {
            // ignored
        }

        return false;
    }

    public static IDisposable SetScopedDirectory(string path) => new ScopedDirectory(path);

    private sealed class ScopedDirectory : IDisposable
    {
        private readonly string _oldDirectory;

        public ScopedDirectory(string path)
        {
            _oldDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(path);
        }

        public void Dispose() => Directory.SetCurrentDirectory(_oldDirectory);
    }
}
