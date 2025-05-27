using System.Security.Cryptography;
using Libplanet.Types;
using Libplanet.Types.Blocks;

namespace Libplanet.State;

public sealed record class BlockExecutionInfo
{
    public required RawBlock Block { get; init; }

    public required World InputWorld { get; init; }

    public required World OutputWorld { get; init; }

    public ImmutableArray<ActionExecutionInfo> BeginExecutions { get; init; } = [];

    public ImmutableArray<TransactionExecutionInfo> Executions { get; init; } = [];

    public ImmutableArray<ActionExecutionInfo> EndExecutions { get; init; } = [];

    public HashDigest<SHA256> StateRootHash => OutputWorld.Trie.Hash;
}
