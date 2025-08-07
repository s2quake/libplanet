namespace Libplanet.Types;

public static class BigIntegerUtility
{
    public static BigInteger Sum(IEnumerable<BigInteger> values)
    {
        BigInteger sum = 0;
        foreach (var value in values)
        {
            sum += value;
        }

        return sum;
    }
}
