using System.Threading.Tasks;

namespace Libplanet.Net;

public interface IRecoverable
{
    Task RecoverAsync();
}
