namespace Libplanet.State.Tests.Actions;

public static class OperatorTypeExtensions
{
    public static Func<BigInteger, BigInteger, BigInteger> ToFunc(this OperatorType @operator) => @operator switch
    {
        OperatorType.Add => BigInteger.Add,
        OperatorType.Sub => BigInteger.Subtract,
        OperatorType.Mul => BigInteger.Multiply,
        OperatorType.Div => BigInteger.Divide,
        OperatorType.Mod => BigInteger.Remainder,
        _ => throw new ArgumentException("Unsupported operator: " + @operator, nameof(@operator)),
    };

    public static BigInteger Calculate(this OperatorType @operator, BigInteger left, BigInteger right)
        => @operator.ToFunc()(left, right);
}
