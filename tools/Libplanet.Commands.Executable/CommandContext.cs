using JSSoft.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Libplanet.Commands.Executable;

public sealed class CommandContext(IServiceProvider serviceProvider)
    : CommandContextBase(serviceProvider.GetServices<ICommand>())
{
}