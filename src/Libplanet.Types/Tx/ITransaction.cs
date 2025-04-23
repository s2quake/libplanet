namespace Libplanet.Types.Tx;

public interface ITransaction : IUnsignedTx
{
    TxId Id { get; }

    public byte[] Signature { get; }
}
