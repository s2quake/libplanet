using Libplanet.Serialization;
using Libplanet.Types.Tx;

namespace Libplanet.Action;

public interface IAction
{
    void Execute(IWorldContext worldContext, IActionContext actionContext);
}
