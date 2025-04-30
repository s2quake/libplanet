namespace Libplanet.Tests;

public static partial class RandomUtility
{
    public static Tuple<T1, T2> Tuple<T1, T2>(Func<T1> generator1, Func<T2> generator2)
    {
        return new Tuple<T1, T2>(generator1(), generator2());
    }

    public static Tuple<T1, T2> Tuple<T1, T2>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2)
    {
        return new Tuple<T1, T2>(generator1(random), generator2(random));
    }

    public static (T1, T2) ValueTuple<T1, T2>(Func<T1> generator1, Func<T2> generator2)
    {
        return (generator1(), generator2());
    }

    public static (T1, T2) ValueTuple<T1, T2>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2)
    {
        return (generator1(random), generator2(random));
    }
}
