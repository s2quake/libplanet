using Libplanet.State;
using Libplanet.Node.Extensions;
using Libplanet.Node.Services;
using Libplanet.Serialization;
using Libplanet.Types;
using Libplanet.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Libplanet.Node.Tests.Services;

public class BlockChainServiceTest
{
    [Fact]
    public void Create_Test()
    {
        var serviceProvider = TestUtility.CreateServiceProvider();
        var blockChainService = serviceProvider.GetRequiredService<IBlockChainService>();
        var blockChain = blockChainService.BlockChain;

        Assert.Equal(1, blockChain.Blocks.Count);
    }

    [Fact]
    public void Create_Using_Genesis_Configuration_Test()
    {
        var genesisKey = new PrivateKey();
        string tempDirectory = Path.GetTempPath();
        string tempFilePath = Path.Combine(tempDirectory, Guid.NewGuid().ToString() + ".json");
        var accountA = Address.Parse("0000000000000000000000000000000000000000");
        var accountB = Address.Parse("0000000000000000000000000000000000000001");
        var addressA = Address.Parse("0000000000000000000000000000000000000000");
        var addressB = Address.Parse("0000000000000000000000000000000000000001");

        try
        {
            string jsonContent = $@"
            {{
                ""{accountA}"": {{
                    ""{addressA}"": ""{ByteUtility.Hex(ModelSerializer.SerializeToBytes("A"))}"",
                    ""{addressB}"": ""{ByteUtility.Hex(ModelSerializer.SerializeToBytes(123))}""
                }},
                ""{accountB}"": {{
                    ""{addressA}"": ""{ByteUtility.Hex(ModelSerializer.SerializeToBytes("B"))}"",
                    ""{addressB}"": ""{ByteUtility.Hex(ModelSerializer.SerializeToBytes(456))}""
                }}
            }}";
            File.WriteAllText(tempFilePath, jsonContent);
            var configDict = new Dictionary<string, string>
            {
                { "Genesis:GenesisConfigurationPath", tempFilePath },
                { "Genesis:GenesisKey", ByteUtility.Hex(genesisKey.Bytes.ToArray()) },
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configDict!)
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
            services.AddLogging();
            services.AddLibplanetNode(configuration);
            var serviceProvider = services.BuildServiceProvider();
            var blockChainService = serviceProvider.GetRequiredService<IBlockChainService>();
            var blockChain = blockChainService.BlockChain;
            var world = blockChain.GetWorld()!;
            Assert.Equal("A", world.GetAccount(accountA).GetValue(addressA));
            Assert.Equal(123, world.GetAccount(accountA).GetValue(addressB));
            Assert.Equal("B", world.GetAccount(accountB).GetValue(addressA));
            Assert.Equal(456, world.GetAccount(accountB).GetValue(addressB));
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }
}
