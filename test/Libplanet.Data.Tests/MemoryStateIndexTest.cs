using System.Security.Cryptography;
using Libplanet.Types;
using Libplanet.TestUtilities;

namespace Libplanet.Data.Tests;

public sealed class MemoryStateIndexTest(ITestOutputHelper output)
    : MemoryIndexTestBase<HashDigest<SHA256>, byte[], StateIndex>(output)
{
    protected override StateIndex CreateIndex(MemoryDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override HashDigest<SHA256> CreateKey(Random random)
        => Rand.HashDigest<SHA256>(random);

    protected override byte[] CreateValue(Random random) => Rand.Bytes(random);
}
