using System;

namespace Libplanet.Types.Tx;

public interface IUnsignedTx : ITxInvoice, ITxSigningMetadata, IEquatable<IUnsignedTx>
{
}
