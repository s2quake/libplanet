using System.Security.Cryptography;
using Libplanet.Types;
using Libplanet.Types.Crypto;

namespace Libplanet.Tests;

public static partial class RandomUtility
{
    public static HashDigest<T> NextHashDigest<T>()
        where T : HashAlgorithm
    {
        return new HashDigest<T>(Array(Byte, HashDigest<T>.Size));
    }

    public static Address Address()
    {
        var bytes = Array(Byte, Types.Crypto.Address.Size);
        return new Address(bytes);
    }
}
