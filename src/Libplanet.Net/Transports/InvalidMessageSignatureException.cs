using Libplanet.Types;

namespace Libplanet.Net.Transports;

public class InvalidMessageSignatureException : Exception
{
    internal InvalidMessageSignatureException(
        string message, BoundPeer peer, PublicKey publicKey, byte[] messageToVerify, byte[] signature)
        : base(message)
    {
        Peer = peer;
        PublicKey = publicKey;
        MessageToVerify = messageToVerify;
        Signature = signature;
    }

    public BoundPeer Peer { get; }

    public PublicKey PublicKey { get; }

    public byte[] MessageToVerify { get; }

    public byte[] Signature { get; }
}
