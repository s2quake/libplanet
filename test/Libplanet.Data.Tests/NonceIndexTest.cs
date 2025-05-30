using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public sealed class NonceIndexTest(ITestOutputHelper output)
    : IndexTestBase<Address, long>(output)
{
    protected override IndexBase<Address, long> CreateIndex(bool useCache)
        => new NonceIndex(new MemoryDatabase(), useCache ? 100 : 0);

    protected override Address CreateKey(Random random) => RandomUtility.Address(random);

    protected override long CreateValue(Random random) => RandomUtility.Int64(random);
}
