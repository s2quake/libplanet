using System.Net;
using Libplanet.Net;

namespace Libplanet.TestUtilities;

public static partial class RandomUtility
{
    public static int Port() => Port(System.Random.Shared);

    public static int Port(Random random) => Int32(random, 0, 65535);

    public static IPAddress IPAddress() => IPAddress(System.Random.Shared);

    public static IPAddress IPAddress(Random random) => new(Array(random, Byte, 4));

    public static DnsEndPoint DnsEndPoint() => DnsEndPoint(System.Random.Shared);

    public static DnsEndPoint DnsEndPoint(Random random) => new(IPAddress(random).ToString(), Port(random));

    public static Protocol Protocol() => Protocol(System.Random.Shared);
    
    public static Protocol Protocol(Random random)
    {
        var signer = PrivateKey(random);
        var version = NonNegative(random);
        var properties = ImmutableSortedDictionary<string, object>(
            keyGenerator: () => String(random),
            valueGenerator: () => String(random));
        return Net.Protocol.Create(signer.AsSigner(), version, properties);
    }
}
