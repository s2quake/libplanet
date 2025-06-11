using Libplanet.Types;

namespace Libplanet.Net.Transports;

public class InvalidMessageSignatureException : Exception
{
    internal InvalidMessageSignatureException(
        string message, Peer peer, Address address, byte[] messageToVerify, byte[] signature)
        : base(message)
    {
        Peer = peer;
        Address = address;
        MessageToVerify = messageToVerify;
        Signature = signature;
    }

    public Peer Peer { get; }

    public Address Address { get; }

    public byte[] MessageToVerify { get; }

    public byte[] Signature { get; }
}
