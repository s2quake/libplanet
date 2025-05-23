using Libplanet.State;
using Libplanet;
using Libplanet.Node.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Libplanet.Node.Tests.Services;

public class RendererServiceTest
{
    [Fact]
    public async Task RenderBlock_TestAsync()
    {
        var serviceProvider = TestUtility.CreateServiceProvider();
        var blockChainService = serviceProvider.GetRequiredService<IBlockChainService>();
        var blockChain = blockChainService.BlockChain;

        using var observer = new TestObserver<RenderBlockInfo>(blockChain.RenderBlock);
        await Assert.RaisesAnyAsync<RenderBlockInfo>(
            attach: handler => observer.Next += handler,
            detach: handler => observer.Next -= handler,
            testCode: async () => await BlockChainUtility.AppendBlockAsync(blockChain));
    }

    [Fact]
    public async Task RenderAction_TestAsync()
    {
        var settings = new Dictionary<string, string?>
        {
            // [$"{ActionOptions.Position}:{nameof(ActionOptions.ModulePath)}"]
            //     = typeof(DumbActionProvider).Assembly.Location,
            // [$"{ActionOptions.Position}:{nameof(ActionOptions.ActionProviderType)}"]
            //     = typeof(DumbActionProvider).FullName,
        };

        var serviceProvider = TestUtility.CreateServiceProvider(settings);
        var blockChainService = serviceProvider.GetRequiredService<IBlockChainService>();
        var blockChain = blockChainService.BlockChain;

        var actions = new IAction[]
        {
            new DumbAction(),
            new DumbAction(),
            new DumbAction(),
        };

        // using var observer = new TestObserver<RenderActionInfo>(blockChain.RenderAction);
        // await Assert.RaisesAnyAsync<RenderActionInfo>(
        //     attach: handler => observer.Next += handler,
        //     detach: handler => observer.Next -= handler,
        //     testCode: async () =>
        //     {
        //         BlockChainUtility.StageTransaction(blockChain, actions);
        //         await BlockChainUtility.AppendBlockAsync(blockChain);
        //     });
    }

    [Fact]
    public async Task RenderActionError_TestAsync()
    {
        var settings = new Dictionary<string, string?>
        {
            // [$"{ActionOptions.Position}:{nameof(ActionOptions.ModulePath)}"]
            //     = typeof(DumbActionProvider).Assembly.Location,
            // [$"{ActionOptions.Position}:{nameof(ActionOptions.ActionProviderType)}"]
            //     = typeof(DumbActionProvider).FullName,
        };

        var serviceProvider = TestUtility.CreateServiceProvider(settings);
        var blockChainService = serviceProvider.GetRequiredService<IBlockChainService>();
        var blockChain = blockChainService.BlockChain;
        var errorMessage = "123";

        var actions = new IAction[]
        {
            new DumbAction() { ErrorMessage = errorMessage },
        };

        // using var observer = new TestObserver<RenderActionInfo>(
        //     blockChain.RenderAction);
        // var errorInfo = await Assert.RaisesAnyAsync<RenderActionInfo>(
        //     attach: handler => observer.Next += handler,
        //     detach: handler => observer.Next -= handler,
        //     testCode: async () =>
        //     {
        //         BlockChainUtility.StageTransaction(blockChain, actions);
        //         await BlockChainUtility.AppendBlockAsync(blockChain);
        //     });
        // Assert.Equal(errorMessage, errorInfo.Arguments.Exception.InnerException!.Message);
    }

    [Fact]
    public async Task RenderBlockEnd_TestAsync()
    {
        var serviceProvider = TestUtility.CreateServiceProvider();
        var blockChainService = serviceProvider.GetRequiredService<IBlockChainService>();
        var blockChain = blockChainService.BlockChain;

        using var observer = new TestObserver<RenderBlockInfo>(blockChain.RenderBlockEnd);
        await Assert.RaisesAnyAsync<RenderBlockInfo>(
            attach: handler => observer.Next += handler,
            detach: handler => observer.Next -= handler,
            testCode: async () => await BlockChainUtility.AppendBlockAsync(blockChain));
    }
}
