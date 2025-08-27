using Libplanet.Types;
using Libplanet.Data;

namespace Libplanet;

public static class BlockBuilderExtensions
{
    public static Block Create(this BlockBuilder @this, ISigner proposer, Blockchain blockchain)
    {
        var tipInfo = blockchain.TipInfo;
        var builder = @this with
        {
            Height = tipInfo.Height + 1,
            PreviousBlockHash = tipInfo.BlockHash,
            PreviousBlockCommit = tipInfo.BlockCommit,
            PreviousStateRootHash = tipInfo.StateRootHash,
        };
        return builder.Create(proposer);
    }

    public static Block Create(this BlockBuilder @this, ISigner proposer, Repository repository)
    {
        var builder = @this with
        {
            Height = repository.Height + 1,
            PreviousBlockHash = repository.BlockHash,
            PreviousBlockCommit = repository.BlockCommit,
            PreviousStateRootHash = repository.StateRootHash,
        };
        return builder.Create(proposer);
    }
}
