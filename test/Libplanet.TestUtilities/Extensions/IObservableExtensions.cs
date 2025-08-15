namespace Libplanet.TestUtilities.Extensions;

public static class IObservableExtensions
{
    public static ObservableEmissionCounter<T> Counter<T>(this IObservable<T> @this) => new(@this);

    public static ObservableEmissionCounter<T> Counter<T>(this IObservable<T> @this, Func<T, bool> predicate)
        => new(@this, predicate);
}
