#pragma warning disable MEN002 // Line is too long

namespace Libplanet.Types.Tests;

public static partial class RandomUtility
{
    public static sbyte? MaybeSByte() => Nullable(SByte);

    public static sbyte? MaybeSByte(Random random) => Nullable(random, SByte);

    public static byte? MaybeByte() => Nullable(Byte);

    public static byte? MaybeByte(Random random) => Nullable(random, Byte);

    public static short? MaybeInt16() => Nullable(Int16);

    public static short? MaybeInt16(Random random) => Nullable(random, Int16);

    public static ushort? MaybeUInt16() => Nullable(UInt16);

    public static ushort? MaybeUInt16(Random random) => Nullable(random, UInt16);

    public static int? MaybeInt32() => Nullable(Int32);

    public static int? MaybeInt32(Random random) => Nullable(random, Int32);

    public static uint? MaybeUInt32() => Nullable(UInt32);

    public static uint? MaybeUInt32(Random random) => Nullable(random, UInt32);

    public static long? MaybeInt64() => Nullable(Int64);

    public static long? MaybeInt64(Random random) => Nullable(random, Int64);

    public static ulong? MaybeUInt64() => Nullable(UInt64);

    public static ulong? MaybeUInt64(Random random) => Nullable(random, UInt64);

    public static float? MaybeSingle() => Nullable(Single);

    public static float? MaybeSingle(Random random) => Nullable(random, Single);

    public static double? MaybeDouble() => Nullable(Double);

    public static double? MaybeDouble(Random random) => Nullable(random, Double);

    public static decimal? MaybeDecimal() => Nullable(Decimal);

    public static decimal? MaybeDecimal(Random random) => Nullable(random, Decimal);

    public static BigInteger? MaybeBigInteger() => Nullable(BigInteger);

    public static BigInteger? MaybeBigInteger(Random random) => Nullable(random, BigInteger);

    public static string? MaybeString() => NullableObject(String);

    public static string? MaybeString(Random random) => NullableObject(random, String);

    public static char? MaybeChar() => Nullable(Char);

    public static char? MaybeChar(Random random) => Nullable(random, Char);

    public static DateTime? MaybeDateTime() => Nullable(DateTime);

    public static DateTime? MaybeDateTime(Random random) => Nullable(random, DateTime);

    public static DateTimeOffset? MaybeDateTimeOffset() => Nullable(DateTimeOffset);

    public static DateTimeOffset? MaybeDateTimeOffset(Random random) => Nullable(random, DateTimeOffset);

    public static TimeSpan? MaybeTimeSpan() => Nullable(TimeSpan);

    public static TimeSpan? MaybeTimeSpan(Random random) => Nullable(random, TimeSpan);

    public static bool? MaybeBoolean() => Nullable(Boolean);

    public static bool? MaybeBoolean(Random random) => Nullable(random, Boolean);

    public static T[]? MaybeArray<T>(Func<T> generator) => NullableObject(() => Array(generator));

    public static T[]? MaybeArray<T>(Random random, Func<Random, T> generator)
        => NullableObject(random, random => Array(random, generator));

    public static List<T>? MaybeList<T>(Func<T> generator) => NullableObject(() => List(generator));

    public static List<T>? MaybeList<T>(Random random, Func<Random, T> generator)
        => NullableObject(random, random => List(random, generator));

    public static ImmutableArray<T>? MaybeImmutableArray<T>(Func<T> generator)
        => Nullable(() => ImmutableArray(generator));

    public static ImmutableArray<T>? MaybeImmutableArray<T>(Random random, Func<Random, T> generator)
        => Nullable(random, random => ImmutableArray(random, generator));

    public static ImmutableList<T>? MaybeImmutableList<T>(Func<T> generator)
        => NullableObject(() => ImmutableList(generator));

    public static ImmutableList<T>? MaybeImmutableList<T>(Random random, Func<Random, T> generator)
        => NullableObject(random, random => ImmutableList(random, generator));

    public static Tuple<T1, T2>? MaybeTuple<T1, T2>(Func<T1> generator1, Func<T2> generator2)
        => NullableObject(() => Tuple(generator1, generator2));

    public static Tuple<T1, T2>? MaybeTuple<T1, T2>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2)
        => NullableObject(random, random => Tuple(random, generator1, generator2));

    public static Tuple<T1, T2, T3>? MaybeTuple<T1, T2, T3>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3)
        => NullableObject(() => Tuple(generator1, generator2, generator3));

    public static Tuple<T1, T2, T3>? MaybeTuple<T1, T2, T3>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3)
        => NullableObject(random, random => Tuple(random, generator1, generator2, generator3));

    public static Tuple<T1, T2, T3, T4>? MaybeTuple<T1, T2, T3, T4>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4)
        => NullableObject(() => Tuple(generator1, generator2, generator3, generator4));

    public static Tuple<T1, T2, T3, T4>? MaybeTuple<T1, T2, T3, T4>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4)
        => NullableObject(random, random => Tuple(random, generator1, generator2, generator3, generator4));

    public static Tuple<T1, T2, T3, T4, T5>? MaybeTuple<T1, T2, T3, T4, T5>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4, Func<T5> generator5)
        => NullableObject(() => Tuple(generator1, generator2, generator3, generator4, generator5));

    public static Tuple<T1, T2, T3, T4, T5>? MaybeTuple<T1, T2, T3, T4, T5>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4, Func<Random, T5> generator5)
        => NullableObject(random, random => Tuple(random, generator1, generator2, generator3, generator4, generator5));

    public static Tuple<T1, T2, T3, T4, T5, T6>? MaybeTuple<T1, T2, T3, T4, T5, T6>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4, Func<T5> generator5, Func<T6> generator6)
        => NullableObject(() => Tuple(generator1, generator2, generator3, generator4, generator5, generator6));

    public static Tuple<T1, T2, T3, T4, T5, T6>? MaybeTuple<T1, T2, T3, T4, T5, T6>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4, Func<Random, T5> generator5, Func<Random, T6> generator6)
        => NullableObject(random, random => Tuple(random, generator1, generator2, generator3, generator4, generator5, generator6));

    public static Tuple<T1, T2, T3, T4, T5, T6, T7>? MaybeTuple<T1, T2, T3, T4, T5, T6, T7>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4, Func<T5> generator5, Func<T6> generator6, Func<T7> generator7)
        => NullableObject(() => Tuple(generator1, generator2, generator3, generator4, generator5, generator6, generator7));

    public static Tuple<T1, T2, T3, T4, T5, T6, T7>? MaybeTuple<T1, T2, T3, T4, T5, T6, T7>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4, Func<Random, T5> generator5, Func<Random, T6> generator6, Func<Random, T7> generator7)
        => NullableObject(random, random => Tuple(random, generator1, generator2, generator3, generator4, generator5, generator6, generator7));

    public static Tuple<T1, T2, T3, T4, T5, T6, T7, TRest>? MaybeTuple<T1, T2, T3, T4, T5, T6, T7, TRest>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4, Func<T5> generator5, Func<T6> generator6, Func<T7> generator7, Func<TRest> generator8)
        where TRest : notnull
        => NullableObject(() => Tuple(generator1, generator2, generator3, generator4, generator5, generator6, generator7, generator8));

    public static Tuple<T1, T2, T3, T4, T5, T6, T7, TRest>? MaybeTuple<T1, T2, T3, T4, T5, T6, T7, TRest>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4, Func<Random, T5> generator5, Func<Random, T6> generator6, Func<Random, T7> generator7, Func<Random, TRest> generator8)
        where TRest : notnull
        => NullableObject(random, random => Tuple(random, generator1, generator2, generator3, generator4, generator5, generator6, generator7, generator8));

    public static (T1, T2)? MaybeValueTuple<T1, T2>(Func<T1> generator1, Func<T2> generator2)
        => Nullable(() => ValueTuple(generator1, generator2));

    public static (T1, T2)? MaybeValueTuple<T1, T2>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2)
        => Nullable(random, random => ValueTuple(random, generator1, generator2));

    public static (T1, T2, T3)? MaybeValueTuple<T1, T2, T3>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3)
        => Nullable(() => ValueTuple(generator1, generator2, generator3));

    public static (T1, T2, T3)? MaybeValueTuple<T1, T2, T3>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3)
        => Nullable(random, random => ValueTuple(random, generator1, generator2, generator3));

    public static (T1, T2, T3, T4)? MaybeValueTuple<T1, T2, T3, T4>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4)
        => Nullable(() => ValueTuple(generator1, generator2, generator3, generator4));

    public static (T1, T2, T3, T4)? MaybeValueTuple<T1, T2, T3, T4>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4)
        => Nullable(random, random => ValueTuple(random, generator1, generator2, generator3, generator4));

    public static (T1, T2, T3, T4, T5)? MaybeValueTuple<T1, T2, T3, T4, T5>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4, Func<T5> generator5)
        => Nullable(() => ValueTuple(generator1, generator2, generator3, generator4, generator5));

    public static (T1, T2, T3, T4, T5)? MaybeValueTuple<T1, T2, T3, T4, T5>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4, Func<Random, T5> generator5)
        => Nullable(random, random => ValueTuple(random, generator1, generator2, generator3, generator4, generator5));

    public static (T1, T2, T3, T4, T5, T6)? MaybeValueTuple<T1, T2, T3, T4, T5, T6>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4, Func<T5> generator5, Func<T6> generator6)
        => Nullable(() => ValueTuple(generator1, generator2, generator3, generator4, generator5, generator6));

    public static (T1, T2, T3, T4, T5, T6)? MaybeValueTuple<T1, T2, T3, T4, T5, T6>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4, Func<Random, T5> generator5, Func<Random, T6> generator6)
        => Nullable(random, random => ValueTuple(random, generator1, generator2, generator3, generator4, generator5, generator6));

    public static (T1, T2, T3, T4, T5, T6, T7)? MaybeValueTuple<T1, T2, T3, T4, T5, T6, T7>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4, Func<T5> generator5, Func<T6> generator6, Func<T7> generator7)
        => Nullable(() => ValueTuple(generator1, generator2, generator3, generator4, generator5, generator6, generator7));

    public static (T1, T2, T3, T4, T5, T6, T7)? MaybeValueTuple<T1, T2, T3, T4, T5, T6, T7>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4, Func<Random, T5> generator5, Func<Random, T6> generator6, Func<Random, T7> generator7)
        => Nullable(random, random => ValueTuple(random, generator1, generator2, generator3, generator4, generator5, generator6, generator7));

    public static (T1, T2, T3, T4, T5, T6, T7, TRest)? MaybeValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4, Func<T5> generator5, Func<T6> generator6, Func<T7> generator7, Func<TRest> generator8)
        where TRest : struct
        => Nullable(() => ValueTuple(generator1, generator2, generator3, generator4, generator5, generator6, generator7, generator8));

    public static (T1, T2, T3, T4, T5, T6, T7, TRest)? MaybeValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4, Func<Random, T5> generator5, Func<Random, T6> generator6, Func<Random, T7> generator7, Func<Random, TRest> generator8)
        where TRest : struct
        => Nullable(random, random => ValueTuple(random, generator1, generator2, generator3, generator4, generator5, generator6, generator7, generator8));

    public static T? Nullable<T>(Func<T> generator)
        where T : struct
    {
        if (Boolean())
        {
            return generator();
        }

        return null;
    }

    public static T? Nullable<T>(Random random, Func<Random, T> generator)
        where T : struct
    {
        if (Boolean(random))
        {
            return generator(random);
        }

        return null;
    }

    public static T? NullableObject<T>(Func<T> generator)
        where T : notnull
    {
        if (Boolean())
        {
            return generator();
        }

        return default;
    }

    public static T? NullableObject<T>(Random random, Func<Random, T> generator)
        where T : notnull
    {
        if (Boolean(random))
        {
            return generator(random);
        }

        return default;
    }

    public static T?[] NullableArray<T>(Func<T> generator)
        where T : struct
    {
        if (Boolean())
        {
            return [];
        }

        var length = Length();
        var items = new T?[length];
        for (var i = 0; i < length; i++)
        {
            items[i] = Nullable(generator);
        }

        return items;
    }

    public static T?[] NullableArray<T>(Random random, Func<Random, T> generator)
        where T : struct
    {
        if (Boolean(random))
        {
            return [];
        }

        var length = Length(random);
        var items = new T?[length];
        for (var i = 0; i < length; i++)
        {
            items[i] = Nullable(random, generator);
        }

        return items;
    }

    public static T?[] NullableObjectArray<T>(Func<T> generator)
        where T : struct
    {
        if (Boolean())
        {
            return [];
        }

        var length = Length();
        var items = new T?[length];
        for (var i = 0; i < length; i++)
        {
            items[i] = NullableObject(generator);
        }

        return items;
    }
}
