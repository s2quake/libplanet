#pragma warning disable MEN002 // Line is too long
#pragma warning disable SA1414 // Tuple types in signatures should have element names
namespace Libplanet.Tests;

public static partial class RandomUtility
{
    public static Tuple<T1, T2> Tuple<T1, T2>(Func<T1> generator1, Func<T2> generator2)
        => new(generator1(), generator2());

    public static Tuple<T1, T2> Tuple<T1, T2>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2)
        => new(generator1(random), generator2(random));

    public static Tuple<T1, T2, T3> Tuple<T1, T2, T3>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3)
        => new(generator1(), generator2(), generator3());

    public static Tuple<T1, T2, T3> Tuple<T1, T2, T3>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3)
        => new(generator1(random), generator2(random), generator3(random));

    public static Tuple<T1, T2, T3, T4> Tuple<T1, T2, T3, T4>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4)
        => new(generator1(), generator2(), generator3(), generator4());

    public static Tuple<T1, T2, T3, T4> Tuple<T1, T2, T3, T4>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4)
        => new(generator1(random), generator2(random), generator3(random), generator4(random));

    public static Tuple<T1, T2, T3, T4, T5> Tuple<T1, T2, T3, T4, T5>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4, Func<T5> generator5)
        => new(generator1(), generator2(), generator3(), generator4(), generator5());

    public static Tuple<T1, T2, T3, T4, T5> Tuple<T1, T2, T3, T4, T5>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4, Func<Random, T5> generator5)
        => new(generator1(random), generator2(random), generator3(random), generator4(random), generator5(random));

    public static Tuple<T1, T2, T3, T4, T5, T6> Tuple<T1, T2, T3, T4, T5, T6>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4, Func<T5> generator5, Func<T6> generator6)
        => new(generator1(), generator2(), generator3(), generator4(), generator5(), generator6());

    public static Tuple<T1, T2, T3, T4, T5, T6> Tuple<T1, T2, T3, T4, T5, T6>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4, Func<Random, T5> generator5, Func<Random, T6> generator6)
        => new(generator1(random), generator2(random), generator3(random), generator4(random), generator5(random), generator6(random));

    public static Tuple<T1, T2, T3, T4, T5, T6, T7> Tuple<T1, T2, T3, T4, T5, T6, T7>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4, Func<T5> generator5, Func<T6> generator6, Func<T7> generator7)
        => new(generator1(), generator2(), generator3(), generator4(), generator5(), generator6(), generator7());

    public static Tuple<T1, T2, T3, T4, T5, T6, T7> Tuple<T1, T2, T3, T4, T5, T6, T7>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4, Func<Random, T5> generator5, Func<Random, T6> generator6, Func<Random, T7> generator7)
        => new(generator1(random), generator2(random), generator3(random), generator4(random), generator5(random), generator6(random), generator7(random));

    public static Tuple<T1, T2, T3, T4, T5, T6, T7, TRest> Tuple<T1, T2, T3, T4, T5, T6, T7, TRest>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4, Func<T5> generator5, Func<T6> generator6, Func<T7> generator7, Func<TRest> generator8)
        where TRest : notnull
        => new(generator1(), generator2(), generator3(), generator4(), generator5(), generator6(), generator7(), generator8());

    public static Tuple<T1, T2, T3, T4, T5, T6, T7, TRest> Tuple<T1, T2, T3, T4, T5, T6, T7, TRest>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4, Func<Random, T5> generator5, Func<Random, T6> generator6, Func<Random, T7> generator7, Func<Random, TRest> generator8)
        where TRest : notnull
        => new(generator1(random), generator2(random), generator3(random), generator4(random), generator5(random), generator6(random), generator7(random), generator8(random));

    public static (T1, T2) ValueTuple<T1, T2>(Func<T1> generator1, Func<T2> generator2)
        => (generator1(), generator2());

    public static (T1, T2) ValueTuple<T1, T2>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2)
        => (generator1(random), generator2(random));

    public static (T1, T2, T3) ValueTuple<T1, T2, T3>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3)
        => (generator1(), generator2(), generator3());

    public static (T1, T2, T3) ValueTuple<T1, T2, T3>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3)
        => (generator1(random), generator2(random), generator3(random));

    public static (T1, T2, T3, T4) ValueTuple<T1, T2, T3, T4>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4)
        => (generator1(), generator2(), generator3(), generator4());

    public static (T1, T2, T3, T4) ValueTuple<T1, T2, T3, T4>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4)
        => (generator1(random), generator2(random), generator3(random), generator4(random));

    public static (T1, T2, T3, T4, T5) ValueTuple<T1, T2, T3, T4, T5>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4, Func<T5> generator5)
        => (generator1(), generator2(), generator3(), generator4(), generator5());

    public static (T1, T2, T3, T4, T5) ValueTuple<T1, T2, T3, T4, T5>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4, Func<Random, T5> generator5)
        => (generator1(random), generator2(random), generator3(random), generator4(random), generator5(random));

    public static (T1, T2, T3, T4, T5, T6) ValueTuple<T1, T2, T3, T4, T5, T6>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4, Func<T5> generator5, Func<T6> generator6)
        => (generator1(), generator2(), generator3(), generator4(), generator5(), generator6());

    public static (T1, T2, T3, T4, T5, T6) ValueTuple<T1, T2, T3, T4, T5, T6>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4, Func<Random, T5> generator5, Func<Random, T6> generator6)
        => (generator1(random), generator2(random), generator3(random), generator4(random), generator5(random), generator6(random));

    public static (T1, T2, T3, T4, T5, T6, T7) ValueTuple<T1, T2, T3, T4, T5, T6, T7>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4, Func<T5> generator5, Func<T6> generator6, Func<T7> generator7)
        => (generator1(), generator2(), generator3(), generator4(), generator5(), generator6(), generator7());

    public static (T1, T2, T3, T4, T5, T6, T7) ValueTuple<T1, T2, T3, T4, T5, T6, T7>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4, Func<Random, T5> generator5, Func<Random, T6> generator6, Func<Random, T7> generator7)
        => (generator1(random), generator2(random), generator3(random), generator4(random), generator5(random), generator6(random), generator7(random));

    public static (T1, T2, T3, T4, T5, T6, T7, TRest) ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4, Func<T5> generator5, Func<T6> generator6, Func<T7> generator7, Func<TRest> generator8)
        where TRest : struct
        => (generator1(), generator2(), generator3(), generator4(), generator5(), generator6(), generator7(), generator8());

    public static (T1, T2, T3, T4, T5, T6, T7, TRest) ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4, Func<Random, T5> generator5, Func<Random, T6> generator6, Func<Random, T7> generator7, Func<Random, TRest> generator8)
        where TRest : struct
        => (generator1(random), generator2(random), generator3(random), generator4(random), generator5(random), generator6(random), generator7(random), generator8(random));
}
