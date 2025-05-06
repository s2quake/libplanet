namespace Libplanet.Tests;

public class AnonymousComparer<T>(Func<T?, T?, int> comparer) : IComparer<T>
{
    public int Compare(T? x, T? y) => comparer(x, y);
}
