using Libplanet.Node.Options;
using Libplanet.Node.Services;
using Libplanet.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Libplanet.Node.Tests.Services;

public class StoreServiceTest
{
    [Fact]
    public void RocksDB_Test()
    {
        var settings = new Dictionary<string, string?>
        {
            [$"{RepositoryOptions.Position}:{nameof(RepositoryOptions.Type)}"] = $"{RepositoryType.RocksDB}",
        };
        var serviceProvider = TestUtility.CreateServiceProvider(settings);
        var storeService = serviceProvider.GetRequiredService<IRepositoryService>();

        Assert.IsType<Repository>(storeService.Repository);
    }

    [Fact]
    public void InMemory_Test()
    {
        var settings = new Dictionary<string, string?>
        {
            [$"{RepositoryOptions.Position}:{nameof(RepositoryOptions.Type)}"] = $"{RepositoryType.InMemory}",
        };
        var serviceProvider = TestUtility.CreateServiceProvider(settings);
        var storeService = serviceProvider.GetRequiredService<IRepositoryService>();

        Assert.IsType<Repository>(storeService.Repository);
    }
}
