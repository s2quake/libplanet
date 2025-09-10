namespace Libplanet.Commands.Services;

public interface IConfigurationService<TConfiguration>
{
    TConfiguration Load();

    void Store(TConfiguration configuration);
}
