using System.Security.Cryptography;
using Libplanet.Data;
using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Commands.Repositories;

[Model("Libplanet_Commands_Repositories_RepositoryInfo", Version = 1)]
public readonly record struct RepositoryInfo
{
    public RepositoryInfo(Repository repository)
    {
        Id = repository.Id;
        GenesisHeight = repository.GenesisHeight;
        Height = repository.Height;
        StateRootHash = repository.StateRootHash;
        GenesisBlockHash = repository.GenesisBlockHash;
        Tip = repository.BlockHash;
        TipVersion = repository.BlockVersion;
    }

    [Property(0)]
    public Guid Id { get; init; }

    [Property(1, EmitDefaultValue = true)]
    public int GenesisHeight { get; init; }

    [Property(2, EmitDefaultValue = true)]
    public int Height { get; init; }

    [Property(3, EmitDefaultValue = true)]
    public HashDigest<SHA256> StateRootHash { get; init; }

    [Property(4)]
    public BlockHash GenesisBlockHash { get; init; }

    [Property(5)]
    public BlockHash Tip { get; init; }

    [Property(6, EmitDefaultValue = true)]
    public int TipVersion { get; init; }
}
