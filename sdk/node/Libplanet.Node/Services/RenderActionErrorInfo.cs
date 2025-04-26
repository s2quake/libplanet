using Bencodex.Types;
using Libplanet.Action;

namespace Libplanet.Node.Services;

public readonly record struct RenderActionErrorInfo(
    IValue Action,
    CommittedActionContext Context,
    Exception Exception);
