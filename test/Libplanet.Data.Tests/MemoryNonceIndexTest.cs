using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public sealed class MemoryNonceIndexTest(ITestOutputHelper output)
    : MemoryIndexTestBase<Address, long, NonceIndex>(output)
{
    protected override Address CreateKey(Random random) => RandomUtility.Address(random);

    protected override long CreateValue(Random random) => RandomUtility.Int64(random);
}
