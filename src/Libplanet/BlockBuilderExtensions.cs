using Libplanet.Types;
using Libplanet.Data;

namespace Libplanet;

public static class BlockBuilderExtensions
{
    public static Block Create(this BlockBuilder @this, PrivateKey proposer, Blockchain blockchain)
    {
        var tipInfo = blockchain.TipInfo;
        var builder = @this with
        {
            Height = tipInfo.Height + 1,
            PreviousHash = tipInfo.BlockHash,
            PreviousCommit = tipInfo.BlockCommit,
            PreviousStateRootHash = tipInfo.StateRootHash,
        };
        return builder.Create(proposer);
    }

    public static Block Create(this BlockBuilder @this, PrivateKey proposer, Repository repository)
    {
        var builder = @this with
        {
            Height = repository.Height + 1,
            PreviousHash = repository.BlockHash,
            PreviousCommit = repository.BlockCommit,
            PreviousStateRootHash = repository.StateRootHash,
        };
        return builder.Create(proposer);
    }
}
