using System.Security.Cryptography;
using Libplanet.Action;
using Libplanet.Types;

namespace Libplanet.Blockchain;

public readonly record struct RenderActionInfo(
    IAction Action,
    IActionContext Context,
    HashDigest<SHA256> NextState,
    Exception? Exception)
{
    public static RenderActionInfo Create(ActionEvaluation evaluation)
    {
        return new RenderActionInfo(
            evaluation.Action,
            evaluation.InputContext,
            evaluation.OutputWorld.Trie.Hash,
            evaluation.Exception);
    }
}
