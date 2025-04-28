using System.Security.Cryptography;
using Libplanet.Common;

namespace Libplanet.Tests;

public static partial class RandomUtility
{
    public static HashDigest<T> NextHashDigest<T>()
        where T : HashAlgorithm
    {
        return new HashDigest<T>(Array(Byte, HashDigest<T>.Size));
    }
}
