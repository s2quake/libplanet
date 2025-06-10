// #pragma warning disable MEN002 // Line is too long

// using Zio;

// namespace Libplanet.Types.Tests;

// public static partial class RandomUtility
// {
//     public static sbyte? NullableSByte() => Nullable(SByte);

//     public static sbyte? NullableSByte(Random random) => Nullable(random, SByte);

//     public static byte? NullableByte() => Nullable(Byte);

//     public static byte? NullableByte(Random random) => Nullable(random, Byte);

//     public static short? NullableInt16() => Nullable(Int16);

//     public static short? NullableInt16(Random random) => Nullable(random, Int16);

//     public static ushort? NullableUInt16() => Nullable(UInt16);

//     public static ushort? NullableUInt16(Random random) => Nullable(random, UInt16);

//     public static int? NullableInt32() => Nullable(Int32);

//     public static int? NullableInt32(Random random) => Nullable(random, Int32);

//     public static uint? NullableUInt32() => Nullable(UInt32);

//     public static uint? NullableUInt32(Random random) => Nullable(random, UInt32);

//     public static long? NullableInt64() => Nullable(Int64);

//     public static long? NullableInt64(Random random) => Nullable(random, Int64);

//     public static ulong? NullableUInt64() => Nullable(UInt64);

//     public static ulong? NullableUInt64(Random random) => Nullable(random, UInt64);

//     public static float? NullableSingle() => Nullable(Single);

//     public static float? NullableSingle(Random random) => Nullable(random, Single);

//     public static double? NullableDouble() => Nullable(Double);

//     public static double? NullableDouble(Random random) => Nullable(random, Double);

//     public static decimal? NullableDecimal() => Nullable(Decimal);

//     public static decimal? NullableDecimal(Random random) => Nullable(random, Decimal);

//     public static BigInteger? NullableBigInteger() => Nullable(BigInteger);

//     public static BigInteger? NullableBigInteger(Random random) => Nullable(random, BigInteger);

//     public static string? NullableString() => NullableObject(String);

//     public static string? NullableString(Random random) => NullableObject(random, String);

//     public static char? NullableChar() => Nullable(Char);

//     public static char? NullableChar(Random random) => Nullable(random, Char);

//     public static DateTimeOffset? NullableDateTimeOffset() => Nullable(DateTimeOffset);

//     public static DateTimeOffset? NullableDateTimeOffset(Random random) => Nullable(random, DateTimeOffset);

//     public static TimeSpan? NullableTimeSpan() => Nullable(TimeSpan);

//     public static TimeSpan? NullableTimeSpan(Random random) => Nullable(random, TimeSpan);

//     public static bool? NullableBoolean() => Nullable(Boolean);

//     public static bool? NullableBoolean(Random random) => Nullable(random, Boolean);

//     public static T[]? NullableArray<T>(Func<T> generator) => NullableObject(() => Array(generator));

//     public static T[]? NullableArray<T>(Random random, Func<Random, T> generator)
//         => NullableObject(random, random => Array(random, generator));

//     public static List<T>? NullableList<T>(Func<T> generator) => NullableObject(() => List(generator));

//     public static List<T>? NullableList<T>(Func<T> generator, int length)
//         => NullableObject(() => List(generator, length));

//     public static List<T>? NullableList<T>(Random random, Func<Random, T> generator)
//         => NullableObject(random, random => List(random, generator));

//     public static List<T>? NullableList<T>(Random random, Func<Random, T> generator, int length)
//         => NullableObject(random, random => List(random, generator, length));

//     public static HashSet<T>? NullableHashSet<T>(Func<T> generator)
//         => NullableObject(() => HashSet(generator));

//     public static HashSet<T>? NullableHashSet<T>(Func<T> generator, int length)
//         => NullableObject(() => HashSet(generator, length));

//     public static HashSet<T>? NullableHashSet<T>(Random random, Func<Random, T> generator)
//         => NullableObject(random, random => HashSet(random, generator));

//     public static HashSet<T>? NullableHashSet<T>(Random random, Func<Random, T> generator, int length)
//         => NullableObject(random, random => HashSet(random, generator, length));

//     public static Dictionary<TKey, TValue>? NullableDictionary<TKey, TValue>(
//         Func<TKey> keyGenerator, Func<TValue> valueGenerator)
//         where TKey : notnull
//         => NullableObject(() => Dictionary(keyGenerator, valueGenerator));

//     public static Dictionary<TKey, TValue>? NullableDictionary<TKey, TValue>(
//         Func<TKey> keyGenerator, Func<TValue> valueGenerator, int length)
//         where TKey : notnull
//         => NullableObject(() => Dictionary(keyGenerator, valueGenerator, length));

//     public static Dictionary<TKey, TValue>? NullableDictionary<TKey, TValue>(
//         Random random, Func<Random, TKey> keyGenerator, Func<Random, TValue> valueGenerator)
//         where TKey : notnull
//         => NullableObject(random, random => Dictionary(random, keyGenerator, valueGenerator));

//     public static Dictionary<TKey, TValue>? NullableDictionary<TKey, TValue>(
//         Random random, Func<Random, TKey> keyGenerator, Func<Random, TValue> valueGenerator, int length)
//         where TKey : notnull
//         => NullableObject(random, random => Dictionary(random, keyGenerator, valueGenerator, length));

//     public static ImmutableArray<T>? NullableImmutableArray<T>(Func<T> generator)
//         => NullableObject(() => ImmutableArray(generator));

//     public static ImmutableArray<T>? NullableImmutableArray<T>(Func<T> generator, int length)
//         => NullableObject(() => ImmutableArray(generator, length));

//     public static ImmutableArray<T>? NullableImmutableArray<T>(Random random, Func<Random, T> generator)
//         => NullableObject(random, random => ImmutableArray(random, generator));

//     public static ImmutableArray<T>? NullableImmutableArray<T>(Random random, Func<Random, T> generator, int length)
//         => NullableObject(random, random => ImmutableArray(random, generator, length));

//     public static ImmutableList<T>? NullableImmutableList<T>(Func<T> generator)
//         => NullableObject(() => ImmutableList(generator));

//     public static ImmutableList<T>? NullableImmutableList<T>(Func<T> generator, int length)
//         => NullableObject(() => ImmutableList(generator, length));

//     public static ImmutableList<T>? NullableImmutableList<T>(Random random, Func<Random, T> generator)
//         => NullableObject(random, random => ImmutableList(random, generator));

//     public static ImmutableList<T>? NullableImmutableList<T>(Random random, Func<Random, T> generator, int length)
//         => NullableObject(random, random => ImmutableList(random, generator, length));

//     public static ImmutableHashSet<T>? NullableImmutableHashSet<T>(Func<T> generator)
//         => NullableObject(() => ImmutableHashSet(generator));

//     public static ImmutableHashSet<T>? NullableImmutableHashSet<T>(Func<T> generator, int length)
//         => NullableObject(() => ImmutableHashSet(generator, length));

//     public static ImmutableHashSet<T>? NullableImmutableHashSet<T>(Random random, Func<Random, T> generator)
//         => NullableObject(random, random => ImmutableHashSet(random, generator));

//     public static ImmutableHashSet<T>? NullableImmutableHashSet<T>(Random random, Func<Random, T> generator, int length)
//         => NullableObject(random, random => ImmutableHashSet(random, generator, length));

//     public static ImmutableSortedSet<T>? NullableImmutableSortedSet<T>(Func<T> generator)
//         => NullableObject(() => ImmutableSortedSet(generator));

//     public static ImmutableSortedSet<T>? NullableImmutableSortedSet<T>(Func<T> generator, int length)
//         => NullableObject(() => ImmutableSortedSet(generator, length));

//     public static ImmutableSortedSet<T>? NullableImmutableSortedSet<T>(Random random, Func<Random, T> generator)
//         => NullableObject(random, random => ImmutableSortedSet(random, generator));

//     public static ImmutableSortedSet<T>? NullableImmutableSortedSet<T>(Random random, Func<Random, T> generator, int length)
//         => NullableObject(random, random => ImmutableSortedSet(random, generator, length));

//     public static ImmutableDictionary<TKey, TValue>? NullableImmutableDictionary<TKey, TValue>(
//         Func<TKey> keyGenerator, Func<TValue> valueGenerator)
//         where TKey : notnull
//         => NullableObject(() => ImmutableDictionary(keyGenerator, valueGenerator));

//     public static ImmutableDictionary<TKey, TValue>? NullableImmutableDictionary<TKey, TValue>(
//         Func<TKey> keyGenerator, Func<TValue> valueGenerator, int length)
//         where TKey : notnull
//         => NullableObject(() => ImmutableDictionary(keyGenerator, valueGenerator, length));

//     public static ImmutableDictionary<TKey, TValue>? NullableImmutableDictionary<TKey, TValue>(
//         Random random, Func<Random, TKey> keyGenerator, Func<Random, TValue> valueGenerator)
//         where TKey : notnull
//         => NullableObject(random, random => ImmutableDictionary(random, keyGenerator, valueGenerator));

//     public static ImmutableDictionary<TKey, TValue>? NullableImmutableDictionary<TKey, TValue>(
//         Random random, Func<Random, TKey> keyGenerator, Func<Random, TValue> valueGenerator, int length)
//         where TKey : notnull
//         => NullableObject(random, random => ImmutableDictionary(random, keyGenerator, valueGenerator, length));

//     public static ImmutableSortedDictionary<TKey, TValue>? NullableImmutableSortedDictionary<TKey, TValue>(
//         Func<TKey> keyGenerator, Func<TValue> valueGenerator)
//         where TKey : notnull
//         => NullableObject(() => ImmutableSortedDictionary(keyGenerator, valueGenerator));

//     public static ImmutableSortedDictionary<TKey, TValue>? NullableImmutableSortedDictionary<TKey, TValue>(
//         Func<TKey> keyGenerator, Func<TValue> valueGenerator, int length)
//         where TKey : notnull
//         => NullableObject(() => ImmutableSortedDictionary(keyGenerator, valueGenerator, length));

//     public static ImmutableSortedDictionary<TKey, TValue>? NullableImmutableSortedDictionary<TKey, TValue>(
//         Random random, Func<Random, TKey> keyGenerator, Func<Random, TValue> valueGenerator)
//         where TKey : notnull
//         => NullableObject(random, random => ImmutableSortedDictionary(random, keyGenerator, valueGenerator));

//     public static ImmutableSortedDictionary<TKey, TValue>? NullableImmutableSortedDictionary<TKey, TValue>(
//         Random random, Func<Random, TKey> keyGenerator, Func<Random, TValue> valueGenerator, int length)
//         where TKey : notnull
//         => NullableObject(random, random => ImmutableSortedDictionary(random, keyGenerator, valueGenerator, length));

//     public static Tuple<T1, T2>? NullableTuple<T1, T2>(Func<T1> generator1, Func<T2> generator2)
//         => NullableObject(() => Tuple(generator1, generator2));

//     public static Tuple<T1, T2>? NullableTuple<T1, T2>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2)
//         => NullableObject(random, random => Tuple(random, generator1, generator2));

//     public static Tuple<T1, T2, T3>? NullableTuple<T1, T2, T3>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3)
//         => NullableObject(() => Tuple(generator1, generator2, generator3));

//     public static Tuple<T1, T2, T3>? NullableTuple<T1, T2, T3>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3)
//         => NullableObject(random, random => Tuple(random, generator1, generator2, generator3));

//     public static Tuple<T1, T2, T3, T4>? NullableTuple<T1, T2, T3, T4>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4)
//         => NullableObject(() => Tuple(generator1, generator2, generator3, generator4));

//     public static Tuple<T1, T2, T3, T4>? NullableTuple<T1, T2, T3, T4>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4)
//         => NullableObject(random, random => Tuple(random, generator1, generator2, generator3, generator4));

//     public static Tuple<T1, T2, T3, T4, T5>? NullableTuple<T1, T2, T3, T4, T5>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4, Func<T5> generator5)
//         => NullableObject(() => Tuple(generator1, generator2, generator3, generator4, generator5));

//     public static Tuple<T1, T2, T3, T4, T5>? NullableTuple<T1, T2, T3, T4, T5>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4, Func<Random, T5> generator5)
//         => NullableObject(random, random => Tuple(random, generator1, generator2, generator3, generator4, generator5));

//     public static Tuple<T1, T2, T3, T4, T5, T6>? NullableTuple<T1, T2, T3, T4, T5, T6>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4, Func<T5> generator5, Func<T6> generator6)
//         => NullableObject(() => Tuple(generator1, generator2, generator3, generator4, generator5, generator6));

//     public static Tuple<T1, T2, T3, T4, T5, T6>? NullableTuple<T1, T2, T3, T4, T5, T6>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4, Func<Random, T5> generator5, Func<Random, T6> generator6)
//         => NullableObject(random, random => Tuple(random, generator1, generator2, generator3, generator4, generator5, generator6));

//     public static Tuple<T1, T2, T3, T4, T5, T6, T7>? NullableTuple<T1, T2, T3, T4, T5, T6, T7>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4, Func<T5> generator5, Func<T6> generator6, Func<T7> generator7)
//         => NullableObject(() => Tuple(generator1, generator2, generator3, generator4, generator5, generator6, generator7));

//     public static Tuple<T1, T2, T3, T4, T5, T6, T7>? NullableTuple<T1, T2, T3, T4, T5, T6, T7>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4, Func<Random, T5> generator5, Func<Random, T6> generator6, Func<Random, T7> generator7)
//         => NullableObject(random, random => Tuple(random, generator1, generator2, generator3, generator4, generator5, generator6, generator7));

//     public static Tuple<T1, T2, T3, T4, T5, T6, T7, TRest>? NullableTuple<T1, T2, T3, T4, T5, T6, T7, TRest>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4, Func<T5> generator5, Func<T6> generator6, Func<T7> generator7, Func<TRest> generator8)
//         where TRest : notnull
//         => NullableObject(() => Tuple(generator1, generator2, generator3, generator4, generator5, generator6, generator7, generator8));

//     public static Tuple<T1, T2, T3, T4, T5, T6, T7, TRest>? NullableTuple<T1, T2, T3, T4, T5, T6, T7, TRest>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4, Func<Random, T5> generator5, Func<Random, T6> generator6, Func<Random, T7> generator7, Func<Random, TRest> generator8)
//         where TRest : notnull
//         => NullableObject(random, random => Tuple(random, generator1, generator2, generator3, generator4, generator5, generator6, generator7, generator8));

//     public static (T1, T2)? NullableValueTuple<T1, T2>(Func<T1> generator1, Func<T2> generator2)
//         => Nullable(() => ValueTuple(generator1, generator2));

//     public static (T1, T2)? NullableValueTuple<T1, T2>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2)
//         => Nullable(random, random => ValueTuple(random, generator1, generator2));

//     public static (T1, T2, T3)? NullableValueTuple<T1, T2, T3>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3)
//         => Nullable(() => ValueTuple(generator1, generator2, generator3));

//     public static (T1, T2, T3)? NullableValueTuple<T1, T2, T3>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3)
//         => Nullable(random, random => ValueTuple(random, generator1, generator2, generator3));

//     public static (T1, T2, T3, T4)? NullableValueTuple<T1, T2, T3, T4>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4)
//         => Nullable(() => ValueTuple(generator1, generator2, generator3, generator4));

//     public static (T1, T2, T3, T4)? NullableValueTuple<T1, T2, T3, T4>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4)
//         => Nullable(random, random => ValueTuple(random, generator1, generator2, generator3, generator4));

//     public static (T1, T2, T3, T4, T5)? NullableValueTuple<T1, T2, T3, T4, T5>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4, Func<T5> generator5)
//         => Nullable(() => ValueTuple(generator1, generator2, generator3, generator4, generator5));

//     public static (T1, T2, T3, T4, T5)? NullableValueTuple<T1, T2, T3, T4, T5>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4, Func<Random, T5> generator5)
//         => Nullable(random, random => ValueTuple(random, generator1, generator2, generator3, generator4, generator5));

//     public static (T1, T2, T3, T4, T5, T6)? NullableValueTuple<T1, T2, T3, T4, T5, T6>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4, Func<T5> generator5, Func<T6> generator6)
//         => Nullable(() => ValueTuple(generator1, generator2, generator3, generator4, generator5, generator6));

//     public static (T1, T2, T3, T4, T5, T6)? NullableValueTuple<T1, T2, T3, T4, T5, T6>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4, Func<Random, T5> generator5, Func<Random, T6> generator6)
//         => Nullable(random, random => ValueTuple(random, generator1, generator2, generator3, generator4, generator5, generator6));

//     public static (T1, T2, T3, T4, T5, T6, T7)? NullableValueTuple<T1, T2, T3, T4, T5, T6, T7>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4, Func<T5> generator5, Func<T6> generator6, Func<T7> generator7)
//         => Nullable(() => ValueTuple(generator1, generator2, generator3, generator4, generator5, generator6, generator7));

//     public static (T1, T2, T3, T4, T5, T6, T7)? NullableValueTuple<T1, T2, T3, T4, T5, T6, T7>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4, Func<Random, T5> generator5, Func<Random, T6> generator6, Func<Random, T7> generator7)
//         => Nullable(random, random => ValueTuple(random, generator1, generator2, generator3, generator4, generator5, generator6, generator7));

//     public static (T1, T2, T3, T4, T5, T6, T7, TRest)? NullableValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest>(Func<T1> generator1, Func<T2> generator2, Func<T3> generator3, Func<T4> generator4, Func<T5> generator5, Func<T6> generator6, Func<T7> generator7, Func<TRest> generator8)
//         where TRest : struct
//         => Nullable(() => ValueTuple(generator1, generator2, generator3, generator4, generator5, generator6, generator7, generator8));

//     public static (T1, T2, T3, T4, T5, T6, T7, TRest)? NullableValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest>(Random random, Func<Random, T1> generator1, Func<Random, T2> generator2, Func<Random, T3> generator3, Func<Random, T4> generator4, Func<Random, T5> generator5, Func<Random, T6> generator6, Func<Random, T7> generator7, Func<Random, TRest> generator8)
//         where TRest : struct
//         => Nullable(random, random => ValueTuple(random, generator1, generator2, generator3, generator4, generator5, generator6, generator7, generator8));

//     public static T? Nullable<T>(Func<T> generator)
//         where T : struct
//         => Chance(66) ? generator() : null;

//     public static T? Nullable<T>(Random random, Func<Random, T> generator)
//         where T : struct
//         => Chance(random, 66) ? generator(random) : null;

//     public static T? NullableObject<T>(Func<T> generator)
//         where T : notnull
//         => Chance(66) ? generator() : default;

//     public static T? NullableObject<T>(Random random, Func<Random, T> generator)
//         where T : notnull
//         => Chance(random, 66) ? generator(random) : default;
// }
