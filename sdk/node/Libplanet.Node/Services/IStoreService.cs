using Libplanet.Data;

namespace Libplanet.Node.Services;

public interface IStoreService
{
    Repository Repository { get; }
}
