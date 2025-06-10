using System.IO;
using System.Reflection;
using System.Text;

namespace Libplanet.TestUtilities;

public static partial class RandomUtility
{
    public const int AttemptCount = 100;

    private static readonly string[] Words = GetWords();

    public static sbyte SByte() => SByte(System.Random.Shared);

    public static sbyte SByte(Random random)
    {
        var bytes = new byte[1];
        random.NextBytes(bytes);
        return (sbyte)bytes[0];
    }

    public static byte Byte() => Byte(System.Random.Shared);

    public static byte Byte(Random random)
    {
        var bytes = new byte[1];
        random.NextBytes(bytes);
        return bytes[0];
    }

    public static byte[] Bytes() => Bytes(System.Random.Shared);

    public static byte[] Bytes(int length) => Bytes(System.Random.Shared, length);

    public static byte[] Bytes(Random random) => Bytes(random, Length(random));

    public static byte[] Bytes(Random random, int length)
    {
        var bytes = new byte[length];
        random.NextBytes(bytes);
        return bytes;
    }

    public static short Int16() => Int16(System.Random.Shared);

    public static short Int16(Random random)
    {
        var bytes = new byte[2];
        random.NextBytes(bytes);
        return BitConverter.ToInt16(bytes, 0);
    }

    public static ushort UInt16() => UInt16(System.Random.Shared);

    public static ushort UInt16(Random random)
    {
        var bytes = new byte[2];
        random.NextBytes(bytes);
        return BitConverter.ToUInt16(bytes, 0);
    }

    public static int Int32() => Int32(System.Random.Shared);

    public static int Int32(int minValue, int maxValue) => System.Random.Shared.Next(minValue, maxValue);

    public static int Int32(Random random) => Int32(random, int.MinValue, int.MaxValue);

    public static int Int32(Random random, int minValue, int maxValue) => random.Next(minValue, maxValue);

    public static uint UInt32() => UInt32(System.Random.Shared);

    public static uint UInt32(Random random)
    {
        var bytes = new byte[4];
        random.NextBytes(bytes);
        return BitConverter.ToUInt32(bytes, 0);
    }

    public static long Int64() => Int64(System.Random.Shared);

    public static long Int64(long minValue, long maxValue) => System.Random.Shared.NextInt64(minValue, maxValue);

    public static long Int64(Random random) => Int64(random, long.MinValue, long.MaxValue);

    public static long Int64(Random random, long minValue, long maxValue) => random.NextInt64(minValue, maxValue);

    public static ulong UInt64() => UInt64(System.Random.Shared);

    public static ulong UInt64(Random random)
    {
        var bytes = new byte[8];
        random.NextBytes(bytes);
        return BitConverter.ToUInt64(bytes, 0);
    }

    public static float Single() => Single(System.Random.Shared);

    public static float Single(Random random)
    {
#if NET6_0_OR_GREATER
        return random.NextSingle();
#else
        return (float)random.NextDouble() * random.Next();
#endif
    }

    public static double Double() => Double(System.Random.Shared);

    public static double Double(Random random) => random.NextDouble();

    public static decimal Decimal() => Decimal(System.Random.Shared);

    public static decimal Decimal(Random random) => (decimal)random.NextDouble();

    public static BigInteger BigInteger() => BigInteger(System.Random.Shared);

    public static BigInteger BigInteger(Random random) => new(random.NextInt64());

    public static int Positive() => Positive(System.Random.Shared);

    public static int Positive(Random random) => Int32(random, 0, int.MaxValue) + 1;

    public static int Negative() => Negative(System.Random.Shared);

    public static int Negative(Random random) => Int32(random, int.MinValue, 0);

    public static int NonPositive() => NonPositive(System.Random.Shared);

    public static int NonPositive(Random random) => Int32(random, int.MinValue, 1);

    public static int NonNegative() => NonNegative(System.Random.Shared);

    public static int NonNegative(Random random) => Int32(random, -1, int.MaxValue) + 1;

    public static long PositiveInt64() => PositiveInt64(System.Random.Shared);

    public static long PositiveInt64(Random random) => Int64(random, 0, long.MaxValue) + 1;

    public static long NegativeInt64() => NegativeInt64(System.Random.Shared);

    public static long NegativeInt64(Random random) => Int64(random, long.MinValue, 0);

    public static long NonPositiveInt64() => NonPositiveInt64(System.Random.Shared);

    public static long NonPositiveInt64(Random random) => Int64(random, long.MinValue, 1);

    public static long NonNegativeInt64() => NonNegativeInt64(System.Random.Shared);

    public static long NonNegativeInt64(Random random) => Int64(random, -1, long.MaxValue) + 1;

    public static string Word() => Word(System.Random.Shared);

    public static string Word(Func<string, bool> predicate)
    {
        for (var i = 0; i < AttemptCount; i++)
        {
            var index = Int32(0, Words.Length);
            var item = Words[index];
            if (predicate(item))
            {
                return item;
            }
        }

        throw new InvalidOperationException("No value was found that matches the condition.");
    }

    public static string Word(Random random) => Word(random, item => true);

    public static string Word(Random random, Func<string, bool> predicate)
    {
        for (var i = 0; i < AttemptCount; i++)
        {
            var index = Int32(random, 0, Words.Length);
            var item = Words[index];
            if (predicate(item))
            {
                return item;
            }
        }

        throw new InvalidOperationException("No value was found that matches the condition.");
    }

    public static char Char() => Char(System.Random.Shared);

    public static char Char(Random random) => (char)UInt16(random);

    public static DateTimeOffset DateTimeOffset() => DateTimeOffset(System.Random.Shared);

    public static DateTimeOffset DateTimeOffset(Random random)
    {
        var minValue = System.DateTime.UnixEpoch.Ticks;
        var maxValue = new DateTime(2050, 12, 31, 0, 0, 0, DateTimeKind.Utc).Ticks;
        var value = random.NextInt64(minValue, maxValue) / 10000000L * 10000000L;
        return new DateTimeOffset(value, System.TimeSpan.Zero);
    }

    public static TimeSpan TimeSpan() => TimeSpan(System.Random.Shared);

    public static TimeSpan TimeSpan(Random random) => new(random.NextInt64(new TimeSpan(365, 0, 0, 0).Ticks));

    public static Guid Guid() => Guid(System.Random.Shared);

    public static Guid Guid(Random random) => new Guid(Array(random, Byte, 16));

    public static int Length() => Length(System.Random.Shared);

    public static int Length(int maxLength) => Length(1, maxLength);

    public static int Length(int minLength, int maxLength) => System.Random.Shared.Next(minLength, maxLength);

    public static int Length(Random random) => Length(random, 0, 10);

    public static int Length(Random random, int maxLength) => Length(random, 1, maxLength);

    public static int Length(Random random, int minLength, int maxLength) => random.Next(minLength, maxLength);

    public static bool Boolean() => Boolean(System.Random.Shared);

    public static bool Boolean(Random random) => Int32(random, 0, 2) == 0;

    public static T Enum<T>()
        where T : Enum
        => Enum<T>(System.Random.Shared);

    public static T Enum<T>(Random random)
        where T : Enum
    {
        if (Attribute.GetCustomAttribute(typeof(Enum), typeof(FlagsAttribute)) is FlagsAttribute)
        {
            throw new InvalidOperationException("flags enum is not supported.");
        }

        var values = System.Enum.GetValues(typeof(T));
        var index = Int32(random, 0, values.Length);
        return (T)values.GetValue(index)!;
    }

    public static string String() => String(System.Random.Shared);

    public static string String(Random random)
    {
        var sb = new StringBuilder();
        var count = Int32(random, 1, 10);
        for (var i = 0; i < count; i++)
        {
            if (sb.Length != 0)
            {
                sb.Append(' ');
            }

            sb.Append(Word(random));
        }

        return sb.ToString();
    }

    public static T[] Array<T>(Func<T> generator) => Array(generator, Length());

    public static T[] Array<T>(Func<T> generator, int length) => Array(System.Random.Shared, _ => generator(), length);

    public static T[] Array<T>(Random random, Func<Random, T> generator) => Array(random, generator, Length(random));

    public static T[] Array<T>(Random random, Func<Random, T> generator, int length)
    {
        var items = new T[length];
        for (var i = 0; i < length; i++)
        {
            items[i] = generator(random);
        }

        return items;
    }

    public static List<T> List<T>(Func<T> generator) => List(generator, Length());

    public static List<T> List<T>(Func<T> generator, int length)
        => List(System.Random.Shared, _ => generator(), length);

    public static List<T> List<T>(Random random, Func<Random, T> generator)
        => List(random, generator, Length(random));

    public static List<T> List<T>(Random random, Func<Random, T> generator, int length)
    {
        var items = new T[length];
        for (var i = 0; i < length; i++)
        {
            items[i] = generator(random);
        }

        return [.. items];
    }

    public static HashSet<TValue> HashSet<TValue>(Func<TValue> generator) => HashSet(generator, Length());

    public static HashSet<TValue> HashSet<TValue>(Func<TValue> generator, int length)
        => HashSet(System.Random.Shared, _ => generator(), length);

    public static HashSet<TValue> HashSet<TValue>(Random random, Func<Random, TValue> generator)
        => HashSet(random, generator, Length(random));

    public static HashSet<TValue> HashSet<TValue>(Random random, Func<Random, TValue> generator, int length)
    {
        var itemList = new List<TValue>(length);
        for (var i = 0; i < length; i++)
        {
            var item = Try(random, generator, item => !itemList.Contains(item));
            itemList.Add(item);
        }

        return [.. itemList];
    }

    public static Dictionary<TKey, TValue> Dictionary<TKey, TValue>(
        Func<TKey> keyGenerator, Func<TValue> valueGenerator)
        where TKey : notnull
        => Dictionary(keyGenerator, valueGenerator, Length());

    public static Dictionary<TKey, TValue> Dictionary<TKey, TValue>(
        Func<TKey> keyGenerator, Func<TValue> valueGenerator, int length)
        where TKey : notnull
        => Dictionary(System.Random.Shared, _ => keyGenerator(), _ => valueGenerator(), length);

    public static Dictionary<TKey, TValue> Dictionary<TKey, TValue>(
        Random random, Func<Random, TKey> keyGenerator, Func<Random, TValue> valueGenerator)
        where TKey : notnull
        => Dictionary(random, keyGenerator, valueGenerator, Length(random));

    public static Dictionary<TKey, TValue> Dictionary<TKey, TValue>(
        Random random, Func<Random, TKey> keyGenerator, Func<Random, TValue> valueGenerator, int length)
        where TKey : notnull
    {
        var keyList = new List<TKey>(length);
        var items = new KeyValuePair<TKey, TValue>[length];
        for (var i = 0; i < length; i++)
        {
            var key = Try(random, keyGenerator, item => !keyList.Contains(item));
            var value = valueGenerator(random);
            items[i] = new(key, value);
            keyList.Add(key);
        }

        return new Dictionary<TKey, TValue>(items);
    }

    public static ImmutableArray<T> ImmutableArray<T>(Func<T> generator)
        => ImmutableArray(generator, Length());

    public static ImmutableArray<T> ImmutableArray<T>(Func<T> generator, int length)
        => ImmutableArray(System.Random.Shared, _ => generator(), length);

    public static ImmutableArray<T> ImmutableArray<T>(Random random, Func<Random, T> generator)
        => ImmutableArray(random, generator, Length(random));

    public static ImmutableArray<T> ImmutableArray<T>(Random random, Func<Random, T> generator, int length)
    {
        var items = new T[length];
        for (var i = 0; i < length; i++)
        {
            items[i] = generator(random);
        }

        return System.Collections.Immutable.ImmutableArray.Create(items);
    }

    public static ImmutableList<T> ImmutableList<T>(Func<T> generator)
        => ImmutableList(generator, Length());

    public static ImmutableList<T> ImmutableList<T>(Func<T> generator, int length)
        => ImmutableList(System.Random.Shared, _ => generator(), length);

    public static ImmutableList<T> ImmutableList<T>(Random random, Func<Random, T> generator)
        => ImmutableList(random, generator, Length(random));

    public static ImmutableList<T> ImmutableList<T>(Random random, Func<Random, T> generator, int length)
    {
        var items = new T[length];
        for (var i = 0; i < length; i++)
        {
            items[i] = generator(random);
        }

        return System.Collections.Immutable.ImmutableList.Create(items);
    }

    public static ImmutableHashSet<T> ImmutableHashSet<T>(Func<T> generator)
        => ImmutableHashSet(generator, Length());

    public static ImmutableHashSet<T> ImmutableHashSet<T>(Func<T> generator, int length)
        => ImmutableHashSet(System.Random.Shared, _ => generator(), length);

    public static ImmutableHashSet<T> ImmutableHashSet<T>(Random random, Func<Random, T> generator)
        => ImmutableHashSet(random, generator, Length(random));

    public static ImmutableHashSet<T> ImmutableHashSet<T>(Random random, Func<Random, T> generator, int length)
    {
        var itemList = new List<T>(length);
        for (var i = 0; i < length; i++)
        {
            var item = Try(random, generator, item => !itemList.Contains(item));
            itemList.Add(item);
        }

        return [.. itemList];
    }

    public static ImmutableSortedSet<T> ImmutableSortedSet<T>(Func<T> generator)
        => ImmutableSortedSet(generator, Length());

    public static ImmutableSortedSet<T> ImmutableSortedSet<T>(Func<T> generator, int length)
        => ImmutableSortedSet(System.Random.Shared, _ => generator(), length);

    public static ImmutableSortedSet<T> ImmutableSortedSet<T>(Random random, Func<Random, T> generator)
        => ImmutableSortedSet(random, generator, Length(random));

    public static ImmutableSortedSet<T> ImmutableSortedSet<T>(Random random, Func<Random, T> generator, int length)
    {
        var itemList = new List<T>(length);
        for (var i = 0; i < length; i++)
        {
            var item = Try(random, generator, item => !itemList.Contains(item));
            itemList.Add(item);
        }

        return [.. itemList];
    }

    public static ImmutableDictionary<TKey, TValue> ImmutableDictionary<TKey, TValue>(
        Func<TKey> keyGenerator, Func<TValue> valueGenerator)
        where TKey : notnull
        => ImmutableDictionary(keyGenerator, valueGenerator, Length());

    public static ImmutableDictionary<TKey, TValue> ImmutableDictionary<TKey, TValue>(
        Func<TKey> keyGenerator, Func<TValue> valueGenerator, int length)
        where TKey : notnull
        => ImmutableDictionary(System.Random.Shared, _ => keyGenerator(), _ => valueGenerator(), length);

    public static ImmutableDictionary<TKey, TValue> ImmutableDictionary<TKey, TValue>(
        Random random, Func<Random, TKey> keyGenerator, Func<Random, TValue> valueGenerator)
        where TKey : notnull
        => ImmutableDictionary(random, keyGenerator, valueGenerator, Length(random));

    public static ImmutableDictionary<TKey, TValue> ImmutableDictionary<TKey, TValue>(
        Random random, Func<Random, TKey> keyGenerator, Func<Random, TValue> valueGenerator, int length)
        where TKey : notnull
    {
        var keyList = new List<TKey>(length);
        var items = new KeyValuePair<TKey, TValue>[length];
        for (var i = 0; i < length; i++)
        {
            var key = Try(random, keyGenerator, item => !keyList.Contains(item));
            var value = valueGenerator(random);
            items[i] = new(key, value);
            keyList.Add(key);
        }

        return System.Collections.Immutable.ImmutableDictionary.CreateRange(items);
    }

    public static ImmutableSortedDictionary<TKey, TValue> ImmutableSortedDictionary<TKey, TValue>(
        Func<TKey> keyGenerator, Func<TValue> valueGenerator)
        where TKey : notnull
        => ImmutableSortedDictionary(keyGenerator, valueGenerator, Length());

    public static ImmutableSortedDictionary<TKey, TValue> ImmutableSortedDictionary<TKey, TValue>(
        Func<TKey> keyGenerator, Func<TValue> valueGenerator, int length)
        where TKey : notnull
        => ImmutableSortedDictionary(System.Random.Shared, _ => keyGenerator(), _ => valueGenerator(), length);

    public static ImmutableSortedDictionary<TKey, TValue> ImmutableSortedDictionary<TKey, TValue>(
        Random random, Func<Random, TKey> keyGenerator, Func<Random, TValue> valueGenerator)
        where TKey : notnull
        => ImmutableSortedDictionary(random, keyGenerator, valueGenerator, Length(random));

    public static ImmutableSortedDictionary<TKey, TValue> ImmutableSortedDictionary<TKey, TValue>(
        Random random, Func<Random, TKey> keyGenerator, Func<Random, TValue> valueGenerator, int length)
        where TKey : notnull
    {
        var keyList = new List<TKey>(length);
        var items = new KeyValuePair<TKey, TValue>[length];
        for (var i = 0; i < length; i++)
        {
            var key = Try(random, keyGenerator, item => !keyList.Contains(item));
            var value = valueGenerator(random);
            items[i] = new(key, value);
            keyList.Add(key);
        }

        return System.Collections.Immutable.ImmutableSortedDictionary.CreateRange(items);
    }

    public static T? RandomOrDefault<T>(this IEnumerable<T> enumerable) => RandomOrDefault(enumerable, item => true);

    public static T? RandomOrDefault<T>(this IEnumerable<T> enumerable, Func<T, bool> predicate)
    {
        var items = enumerable.Where(predicate).ToArray();
        if (items.Length == 0)
        {
            return default!;
        }

        var count = items.Length;
        var index = Int32(0, count);
        return items[index];
    }

    public static T Random<T>(this IEnumerable<T> enumerable) => Random(enumerable, item => true);

    public static T Random<T>(this IEnumerable<T> enumerable, Func<T, bool> predicate)
    {
        if (!enumerable.Any())
        {
            throw new InvalidOperationException(
                "there is no random item that matches the condition.");
        }

        return RandomOrDefault(enumerable, predicate)!;
    }

    public static T Try<T>(Func<T> generator, Func<T, bool> predicate)
        => Try(System.Random.Shared, _ => generator(), predicate);

    public static T Try<T>(Random random, Func<Random, T> generator, Func<T, bool> predicate)
    {
        var countByValue = new Dictionary<object, int>();
        while (true)
        {
            var item = generator(random);
            if (predicate(item))
            {
                return item;
            }

            var key = item is null ? (object)DBNull.Value : item;
            if (!countByValue.TryGetValue(key, out var count))
            {
                countByValue[key] = count = 0;
            }

            count++;
            if (count >= AttemptCount)
            {
                throw new InvalidOperationException(
                $"No value was found that matches the condition after {AttemptCount} attempts.");
            }

            countByValue[key] = count;
        }
    }

    public static bool Chance(int probability) => Chance(System.Random.Shared, probability);

    public static bool Chance(Random random, int probability)
    {
        if (probability < 0 || probability > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(probability), "Probability must be between 0 and 100.");
        }

        return random.Next(0, 100) < probability;
    }

    public static bool Chance(double probability) => Chance(System.Random.Shared, probability);

    public static bool Chance(Random random, double probability)
    {
        if (probability < 0.0 || probability > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(probability), "Probability must be between 0 and 1.");
        }

        return random.NextDouble() < probability;
    }

    public static IOrderedEnumerable<T> Shuffle<T>(IEnumerable<T> source)
        => Shuffle(System.Random.Shared, source);

    public static IOrderedEnumerable<T> Shuffle<T>(Random random, IEnumerable<T> source)
        => source.OrderBy(_ => random.Next());

    private static string[] GetWords()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = string.Join(
            ".", typeof(RandomUtility).Namespace, "Resources", "words.txt");
        var resourceStream = assembly.GetManifestResourceStream(resourceName)!;
        using var stream = new StreamReader(resourceStream);
        var text = stream.ReadToEnd();
        var i = 0;
        using var sr1 = new StringReader(text);
        while (sr1.ReadLine() is not null)
        {
            i++;
        }

        var words = new string[i];
        i = 0;
        using var sr2 = new StringReader(text);
        while (sr2.ReadLine() is string line2)
        {
            words[i++] = line2;
        }

        return words;
    }
}
