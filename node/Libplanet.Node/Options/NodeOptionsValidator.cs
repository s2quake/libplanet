using Microsoft.Extensions.Options;

namespace Libplanet.Node.Options;

internal sealed class NodeOptionsValidator
    : OptionsValidatorBase<NodeOptions>
{
    protected override void OnValidate(string? name, NodeOptions options)
    {
    }
}
