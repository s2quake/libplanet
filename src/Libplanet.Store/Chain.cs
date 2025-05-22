// using System.Security.Cryptography;
// using Libplanet.Types;
// using Libplanet.Types.Blocks;
// using Libplanet.Types.Crypto;

// namespace Libplanet.Store;

// public sealed class Chain : IDisposable, IHasKey<Guid>
// {
//     private bool _disposed;

//     public Chain(Guid chainId, IDatabase database)
//     {
//         Id = chainId;
//         Metadata = new MetadataStore(chainId, database);
//         BlockHashes = new BlockHashStore(this, database);
//         Nonces = new NonceStore(this, database);
//     }

//     public BlockHashStore BlockHashes { get; }

//     public NonceStore Nonces { get; }

//     public MetadataStore Metadata { get; }

//     public Guid Id { get; }

//     public BlockCommit BlockCommit { get; set; } = BlockCommit.Empty;

//     public HashDigest<SHA256> StateRootHash { get; set; }

//     public int GenesisHeight
//     {
//         get => BlockHashes.GenesisHeight;
//         set => BlockHashes.GenesisHeight = value;
//     }

//     public BlockHash GenesisBlockHash => BlockHashes[GenesisHeight];

//     public BlockHash BlockHash => BlockHashes[Height];

//     public int Height => BlockHashes.Height;

//     Guid IHasKey<Guid>.Key => Id;

//     public void Append(Block block, BlockCommit blockCommit)
//     {
//         BlockHashes.Add(block);
//         BlockCommit = blockCommit;
//         Nonces.Increase(block);
//     }

//     public long GetNonce(Address address) => Nonces.GetValueOrDefault(address);

//     public long IncreaseNonce(Address address, long delta = 1L) => Nonces.Increase(address, delta);

//     public void ForkFrom(Chain source) => ForkFrom(source, default);

//     public void ForkFrom(Chain source, BlockHash branchPoint)
//     {
//         var genesisHash = source.BlockHashes.IterateHeights(0, 1).FirstOrDefault();
//         if (genesisHash == default || branchPoint == genesisHash)
//         {
//             return;
//         }

//         for (var i = source.GenesisHeight; i <= source.Height; i++)
//         {
//             var blockHash = source.BlockHashes[i];
//             BlockHashes[i] = blockHash;
//             if (blockHash.Equals(branchPoint))
//             {
//                 break;
//             }
//         }
//     }

//     public void Dispose()
//     {
//         if (!_disposed)
//         {
//             BlockHashes.Dispose();
//             Nonces.Dispose();
//             _disposed = true;
//             GC.SuppressFinalize(this);
//         }
//     }
// }
