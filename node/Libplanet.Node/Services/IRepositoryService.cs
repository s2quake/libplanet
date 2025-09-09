using Libplanet.Node.Options;

namespace Libplanet.Node.Services;

public interface IRepositoryService
{
    RepositoryType Type { get; }
}
