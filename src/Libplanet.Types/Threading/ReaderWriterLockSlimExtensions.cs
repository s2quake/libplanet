using System.Threading;

namespace Libplanet.Types.Threading;

public static class ReaderWriterLockSlimExtensions
{
    public static IDisposable WriteScope(this ReaderWriterLockSlim @this) => new WriteScope(@this);

    public static IDisposable ReadScope(this ReaderWriterLockSlim @this) => new ReadScope(@this);
}
