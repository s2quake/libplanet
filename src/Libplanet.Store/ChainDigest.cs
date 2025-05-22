// using Libplanet.Serialization;
// using Libplanet.Types.Blocks;

// namespace Libplanet.Store;

// [Model(Version = 1)]
// public sealed record class ChainDigest : IEquatable<ChainDigest>
// {
//     [Property(0)]
//     public required Guid Id { get; init; }

//     [Property(1)]
//     public BlockCommit BlockCommit { get; init; } = BlockCommit.Empty;

//     [Property(2)]
//     public int GenesisHeight { get; init; }

//     [Property(3)]
//     public int Height { get; init; }
// }
