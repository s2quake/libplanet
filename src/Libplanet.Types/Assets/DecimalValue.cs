using System.Numerics;

namespace Libplanet.Types.Assets;

public readonly record struct DecimalValue(int DecimalPlaces, BigInteger RawValue)
{
    public BigInteger Major => RawValue / BigInteger.Pow(10, DecimalPlaces);

    public BigInteger Minor => RawValue % BigInteger.Pow(10, DecimalPlaces);
}
