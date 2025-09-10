using System.IO;

namespace Libplanet.Commands.IO;

public static class FileUtility
{
    public static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                return true;
            }
        }
        catch
        {
            // ignored
        }

        return false;
    }
}
