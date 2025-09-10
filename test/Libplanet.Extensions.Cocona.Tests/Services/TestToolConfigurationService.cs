using Libplanet.Commands.Configuration;
using Libplanet.Commands.Services;

namespace Libplanet.Commands.Executable.Tests.Services;

public class TestToolConfigurationService : IConfigurationService<ToolConfiguration>
{
    public TestToolConfigurationService(ToolConfiguration configuration)
    {
        Configuration = configuration;
    }

    private ToolConfiguration Configuration { get; set; }

    public ToolConfiguration Load()
    {
        return Configuration;
    }

    public void Store(ToolConfiguration configuration)
    {
        Configuration = configuration;
    }
}
