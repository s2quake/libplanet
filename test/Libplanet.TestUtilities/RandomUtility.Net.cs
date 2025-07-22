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

    public static Peer Peer() => Peer(System.Random.Shared);

    public static Peer Peer(Random random) => new()
    {
        Address = Address(random),
        EndPoint = DnsEndPoint(random),
    };

    public static Peer LocalPeer() => LocalPeer(System.Random.Shared);

    public static Peer LocalPeer(Random random) => new()
    {
        Address = Address(random),
        EndPoint = new System.Net.DnsEndPoint("127.0.0.1", Port(random)),
    };

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
    
    public static MessageId MessageId() => MessageId(System.Random.Shared);

    public static MessageId MessageId(Random random) => new(Array(random, Byte, Net.MessageId.Size));
}
