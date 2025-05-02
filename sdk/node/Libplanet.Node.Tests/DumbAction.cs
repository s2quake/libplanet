using Libplanet.Action;
using Libplanet.Serialization;

namespace Libplanet.Node.Tests;

[Model(Version = 1)]
public sealed record class DumbAction : ActionBase
{
    [Property(0)]
    public string ErrorMessage { get; set; } = string.Empty;

    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        if (ErrorMessage != string.Empty)
        {
            throw new InvalidOperationException(ErrorMessage);
        }
    }
}
