using System.Security.Cryptography;
using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Data.RocksDB.Tests;

public sealed class RocksStateRootHashIndexTest(ITestOutputHelper output)
    : RocksIndexTestBase<BlockHash, HashDigest<SHA256>, StateRootHashIndex>(output)
{
    protected override StateRootHashIndex CreateIndex(RocksDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override BlockHash CreateKey(Random random) => Rand.BlockHash(random);

    protected override HashDigest<SHA256> CreateValue(Random random) => Rand.HashDigest<SHA256>(random);
}
