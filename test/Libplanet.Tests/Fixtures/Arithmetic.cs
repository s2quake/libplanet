using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Serialization;

namespace Libplanet.Tests.Fixtures;

[Model(Version = 1)]
public sealed record class Arithmetic : ActionBase
{
    [Property(0)]
    public OperatorType Operator { get; init; }

    [Property(1)]
    public BigInteger Operand { get; init; }

    public static Arithmetic Create(OperatorType @operator, BigInteger operand) => new()
    {
        Operator = @operator,
        Operand = operand,
    };

    public static Arithmetic Add(BigInteger operand) => Create(OperatorType.Add, operand);

    public static Arithmetic Sub(BigInteger operand) => Create(OperatorType.Sub, operand);

    public static Arithmetic Mul(BigInteger operand) => Create(OperatorType.Mul, operand);

    public static Arithmetic Div(BigInteger operand) => Create(OperatorType.Div, operand);

    public static Arithmetic Mod(BigInteger operand) => Create(OperatorType.Mod, operand);

    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        var key = (ReservedAddresses.LegacyAccount, context.Signer);
        var value = world.GetValue(key, (Integer)0);
        world[key] = Operator.Calculate(value, Operand);
    }
}
