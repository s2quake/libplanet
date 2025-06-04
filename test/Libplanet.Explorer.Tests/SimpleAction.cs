using Libplanet.State;
using Libplanet.Serialization;

namespace Libplanet.Explorer.Tests;

public abstract record class SimpleAction : ActionBase
{
    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
    }

    public static SimpleAction GetAction(int seed) =>
        (seed % 10) switch
        {
            1 => new SimpleAction1(),
            2 => new SimpleAction2(),
            3 => new SimpleAction3(),
            4 => new SimpleAction4(),
            5 => new SimpleAction5(),
            6 => new SimpleAction6(),
            7 => new SimpleAction7(),
            8 => new SimpleAction8(),
            9 => new SimpleAction0Fail(),
            _ => new SimpleAction0(),
        };
}

[Model(Version = 1, TypeName = "Libplanet.Explorer.Tests.SimpleAction0")]
public sealed record class SimpleAction0 : SimpleAction
{
}

[Model(Version = 1, TypeName = "Libplanet.Explorer.Tests.SimpleAction1")]
public sealed record class SimpleAction1 : SimpleAction
{
}

[Model(Version = 1, TypeName = "Libplanet.Explorer.Tests.SimpleAction2")]
public sealed record class SimpleAction2 : SimpleAction
{
}

[Model(Version = 1, TypeName = "Libplanet.Explorer.Tests.SimpleAction3")]
public sealed record class SimpleAction3 : SimpleAction
{
}

[Model(Version = 1, TypeName = "Libplanet.Explorer.Tests.SimpleAction4")]
public sealed record class SimpleAction4 : SimpleAction
{
}

[Model(Version = 1, TypeName = "Libplanet.Explorer.Tests.SimpleAction5")]
public sealed record class SimpleAction5 : SimpleAction
{
}

[Model(Version = 1, TypeName = "Libplanet.Explorer.Tests.SimpleAction6")]
public sealed record class SimpleAction6 : SimpleAction
{
}

[Model(Version = 1, TypeName = "Libplanet.Explorer.Tests.SimpleAction7")]
public sealed record class SimpleAction7 : SimpleAction
{
}

[Model(Version = 1, TypeName = "Libplanet.Explorer.Tests.SimpleAction8")]
public sealed record class SimpleAction8 : SimpleAction
{
}

// For overlapping custom action id test and fail test
[Model(Version = 1, TypeName = "Libplanet.Explorer.Tests.SimpleAction0Fail")]
public sealed record class SimpleAction0Fail : SimpleAction
{
    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        throw new CurrencyPermissionException("test message", context.Signer, default);
    }
}
