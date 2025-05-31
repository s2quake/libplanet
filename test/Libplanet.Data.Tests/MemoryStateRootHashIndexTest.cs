using System.Security.Cryptography;
using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public sealed class MemoryStateRootHashIndexTest(ITestOutputHelper output)
    : MemoryIndexTestBase<BlockHash, HashDigest<SHA256>, StateRootHashIndex>(output)
{
    protected override BlockHash CreateKey(Random random) => RandomUtility.BlockHash(random);

    protected override HashDigest<SHA256> CreateValue(Random random) => RandomUtility.HashDigest<SHA256>(random);
}
