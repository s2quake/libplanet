namespace Libplanet.TestUtilities;

public static class IObservableExtensions
{
    public static ObservableEmissionCounter<T> Counter<T>(this IObservable<T> @this) => new(@this);

    public static ObservableEmissionCounter<T> Counter<T>(this IObservable<T> @this, Func<T, bool> predicate)
        => new(@this, predicate);

    public static ObservableWaiter<T> AsWaiter<T>(this IObservable<T> @this, CancellationToken cancellationToken)
        where T : notnull
        => new(@this, cancellationToken);

    public static ObservableWaiter<T> AsWaiter<T>(
        this IObservable<T> @this, Func<T, bool> predicate, CancellationToken cancellationToken)
        where T : notnull
        => new(@this, predicate, cancellationToken);
}
