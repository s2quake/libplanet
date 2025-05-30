using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public class NonceIndexTest(ITestOutputHelper output)
    : IndexTestBase<Address, long, NonceIndex>(output)
{
    protected override NonceIndex CreateIndex(string name, bool useCache)
        => new(new MemoryDatabase(), useCache ? 100 : 0);

    protected override Address CreateKey(Random random) => RandomUtility.Address(random);

    protected override long CreateValue(Random random) => RandomUtility.Int64(random);
}
