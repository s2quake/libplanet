using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public sealed class MemoryStateIndexTest(ITestOutputHelper output) : StateIndexTestBase<MemoryTable>(output)
{
    protected override MemoryTable CreateTableOverride(string name) => new(name);

    protected override void DeleteTableOverride(MemoryTable table)
    {
    }
}
