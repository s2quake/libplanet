#pragma warning disable MEN002 // Line is too long

namespace Libplanet.Tests;

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
        => NullableObject(() => ImmutableArray(generator));

    public static ImmutableArray<T>? MaybeImmutableArray<T>(Random random, Func<Random, T> generator)
        => NullableObject(random, random => ImmutableArray(random, generator));

    public static ImmutableList<T>? MaybeImmutableList<T>(Func<T> generator)
        => NullableObject(() => ImmutableList(generator));

    public static ImmutableList<T>? MaybeImmutableList<T>(Random random, Func<Random, T> generator)
        => NullableObject(random, random => ImmutableList(random, generator));

    public static ValueTuple<T1, T2>? MaybeValueTuple<T1, T2>(Func<T1> generator1, Func<T2> generator2)
        => Nullable(() => ValueTuple(generator1, generator2));

    public static ValueTuple<T1, T2>? MaybeValueTuple<T1, T2>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2)
        => Nullable(random, random => ValueTuple(random, generator1, generator2));

    public static T? Nullable<T>(Func<T> generator)
        where T : struct
    {
        if (Boolean() is true)
        {
            return generator();
        }

        return null;
    }

    public static T? Nullable<T>(Random random, Func<Random, T> generator)
        where T : struct
    {
        if (Boolean(random) is true)
        {
            return generator(random);
        }

        return null;
    }

    public static T? NullableObject<T>(Func<T> generator)
        where T : notnull
    {
        if (Boolean() is true)
        {
            return generator();
        }

        return default;
    }

    public static T? NullableObject<T>(Random random, Func<Random, T> generator)
        where T : notnull
    {
        if (Boolean(random) is true)
        {
            return generator(random);
        }

        return default;
    }

    public static T?[] NullableArray<T>(Func<T> generator)
        where T : struct
    {
        if (Boolean() is true)
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
        if (Boolean(random) is true)
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
        if (Boolean() is true)
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
