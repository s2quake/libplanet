using System.Net;

namespace Libplanet.TestUtilities;

public static partial class RandomUtility
{
    public static int Port() => Port(System.Random.Shared);

    public static int Port(Random random) => Int32(random, 0, 65535);

    public static IPAddress IPAddress() => IPAddress(System.Random.Shared);

    public static IPAddress IPAddress(Random random) => new(Array(random, Byte, 4));

    public static DnsEndPoint DnsEndPoint() => DnsEndPoint(System.Random.Shared);

    public static DnsEndPoint DnsEndPoint(Random random) => new(IPAddress(random).ToString(), Port(random));
}
