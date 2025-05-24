using System.Globalization;
using System.Net;
using Destructurama.Attributed;
using Libplanet.Types.Crypto;

namespace Libplanet.Net;

public sealed record class BoundPeer : IEquatable<BoundPeer>
{
    public BoundPeer(
        PublicKey publicKey,
        DnsEndPoint endPoint)
        : this(publicKey, endPoint, null)
    {
    }

    internal BoundPeer(
        PublicKey publicKey,
        DnsEndPoint endPoint,
        IPAddress? publicIPAddress)
    {
        if (Uri.CheckHostName(endPoint.Host) == UriHostNameType.Unknown)
        {
            throw new ArgumentException(
                $"Given {nameof(endPoint)} has unknown host name type: {endPoint.Host}",
                nameof(endPoint));
        }

        PublicKey = publicKey;
        EndPoint = endPoint;
        PublicIPAddress = publicIPAddress;
    }

    [LogAsScalar]
    public PublicKey PublicKey { get; }

    [LogAsScalar]
    public Address Address => new Address(PublicKey);

    [LogAsScalar]
    public DnsEndPoint EndPoint { get; }

    [LogAsScalar]
    public IPAddress? PublicIPAddress { get; }

    public string PeerString => $"{PublicKey},{EndPoint.Host},{EndPoint.Port}";

    // public static bool operator ==(BoundPeer left, BoundPeer right) => left.Equals(right);

    // public static bool operator !=(BoundPeer left, BoundPeer right) => !left.Equals(right);

    public static BoundPeer ParsePeer(string peerInfo)
    {
        string[] tokens = peerInfo.Split(',');
        if (tokens.Length != 3)
        {
            throw new ArgumentException(
                $"'{peerInfo}', should have format <pubkey>,<host>,<port>",
                nameof(peerInfo));
        }

        if (!(tokens[0].Length == 130 || tokens[0].Length == 66))
        {
            throw new ArgumentException(
                $"'{peerInfo}', a length of public key must be 130 or 66 in hexadecimal," +
                $" but the length of given public key '{tokens[0]}' doesn't.",
                nameof(peerInfo));
        }

        try
        {
            var pubKey = PublicKey.Parse(tokens[0]);
            var host = tokens[1];
            var port = int.Parse(tokens[2], CultureInfo.InvariantCulture);

            // FIXME: It might be better to make Peer.AppProtocolVersion property nullable...
            return new BoundPeer(
                pubKey,
                new DnsEndPoint(host, port));
        }
        catch (Exception e)
        {
            throw new ArgumentException(
                $"{nameof(peerInfo)} seems invalid. [{peerInfo}]",
                nameof(peerInfo),
                innerException: e);
        }
    }

    public bool Equals(BoundPeer? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return PublicKey.Equals(other.PublicKey) &&
            (PublicIPAddress?.Equals(other.PublicIPAddress) ?? other.PublicIPAddress is null) &&
            EndPoint.Equals(other.EndPoint);
    }

    // public override bool Equals(object? obj) => obj is BoundPeer other && Equals(other);

    // public override int GetHashCode() => HashCode.Combine(
    //     HashCode.Combine(PublicKey.GetHashCode(), PublicIPAddress?.GetHashCode()), EndPoint);

    public override string ToString()
    {
        return $"{Address}.{EndPoint}.{PublicIPAddress}";
    }
}
