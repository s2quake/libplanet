using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Libplanet.Serialization.Yaml;

internal sealed class NodeTypeResolver : INodeTypeResolver
{
    public bool Resolve(NodeEvent? nodeEvent, ref Type currentType)
    {
        if (nodeEvent is Scalar { Style: ScalarStyle.Plain, Value: "null" })
        {
            return false;
        }

        if (currentType != typeof(object))
        {
            return false;
        }

        return true;
    }
}
