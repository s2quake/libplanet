using System.Security.Cryptography;
using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Data.LiteDB.Tests;

public sealed class LiteStateRootHashIndexTest(ITestOutputHelper output)
    : LiteIndexTestBase<BlockHash, HashDigest<SHA256>, StateRootHashIndex>(output)
{
    protected override StateRootHashIndex CreateIndex(LiteDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override BlockHash CreateKey(Random random) => Rand.BlockHash(random);

    protected override HashDigest<SHA256> CreateValue(Random random) => Rand.HashDigest<SHA256>(random);
}
