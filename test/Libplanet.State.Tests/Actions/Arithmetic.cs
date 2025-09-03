using Libplanet.Serialization;
using static Libplanet.State.SystemAddresses;

namespace Libplanet.State.Tests.Actions;

[Model(Version = 1, TypeName = "Libplanet_Tests_Fixtures_Arithmetic")]
public sealed record class Arithmetic : ActionBase
{
    [Property(0)]
    public OperatorType Operator { get; init; }

    [Property(1)]
    public BigInteger Operand { get; init; }

    [Property(2)]
    public string Error { get; init; } = string.Empty;

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
        if (Error != string.Empty)
        {
            throw new InvalidOperationException(Error);
        }

        var value = world.GetValueOrDefault(SystemAccount, context.Signer, BigInteger.Zero);
        world[SystemAccount, context.Signer] = Operator.Calculate(value, Operand);
    }
}
