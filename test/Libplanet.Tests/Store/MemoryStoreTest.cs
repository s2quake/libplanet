namespace Libplanet.Tests.Store;

public class MemoryStoreTest : RepositoryTest, IDisposable
{
    public MemoryStoreTest()
    {
        Fx = new MemoryRepositoryFixture();
        FxConstructor = () => new MemoryRepositoryFixture();
    }

    protected override RepositoryFixture Fx { get; }

    protected override Func<RepositoryFixture> FxConstructor { get; }

    public void Dispose()
    {
        Fx?.Dispose();
    }
}
