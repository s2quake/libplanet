using System.IO;
using System.Reflection;
using System.Text;

namespace Libplanet.Tests;

public static partial class RandomUtility
{
    public const int AttemptCount = 10;

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

    public static int Int32(int minValue, int maxValue)
        => System.Random.Shared.Next(minValue, maxValue);

    public static int Int32(Random random) => Int32(random, int.MinValue, int.MaxValue);

    public static int Int32(Random random, int minValue, int maxValue)
        => random.Next(minValue, maxValue);

    public static uint UInt32() => UInt32(System.Random.Shared);

    public static uint UInt32(Random random)
    {
        var bytes = new byte[4];
        random.NextBytes(bytes);
        return BitConverter.ToUInt32(bytes, 0);
    }

    public static long Int64() => Int64(System.Random.Shared);

    public static long Int64(Random random)
    {
        var bytes = new byte[8];
        random.NextBytes(bytes);
        return BitConverter.ToInt64(bytes, 0);
    }

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

    public static string Word() => Word(System.Random.Shared);

    public static string Word(Func<string, bool> predicate)
    {
        for (var i = 0; i < AttemptCount; i++)
        {
            var index = Int32(0, Words.Length);
            var item = Words[index];
            if (predicate(item) is true)
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
            if (predicate(item) is true)
            {
                return item;
            }
        }

        throw new InvalidOperationException("No value was found that matches the condition.");
    }

    public static char Char() => Char(System.Random.Shared);

    public static char Char(Random random) => (char)UInt16(random);

    public static DateTime DateTime() => DateTime(System.Random.Shared);

    public static DateTime DateTime(Random random)
    {
        var minValue = System.DateTime.UnixEpoch.Ticks;
        var maxValue = new DateTime(2050, 12, 31, 0, 0, 0, DateTimeKind.Utc).Ticks;
        var value = random.NextInt64(minValue, maxValue) / 10000000L * 10000000L;
        return new DateTime(value, DateTimeKind.Utc);
    }

    public static DateTimeOffset DateTimeOffset() => DateTimeOffset(System.Random.Shared);

    public static DateTimeOffset DateTimeOffset(Random random)
    {
        var minValue = System.DateTime.UnixEpoch.Ticks;
        var maxValue = new DateTime(2050, 12, 31, 0, 0, 0, DateTimeKind.Utc).Ticks;
        var value = random.NextInt64(minValue, maxValue) / 10000000L * 10000000L;
        return new DateTimeOffset(value, System.TimeSpan.Zero);
    }

    public static TimeSpan TimeSpan() => TimeSpan(System.Random.Shared);

    public static TimeSpan TimeSpan(Random random)
    {
        return new TimeSpan(random.NextInt64(new TimeSpan(365, 0, 0, 0).Ticks));
    }

    public static int Length() => Length(System.Random.Shared);

    public static int Length(int maxLength) => Length(1, maxLength);

    public static int Length(int minLength, int maxLength)
        => System.Random.Shared.Next(minLength, maxLength);

    public static int Length(Random random) => Length(random, 0, 10);

    public static int Length(Random random, int maxLength) => Length(random, 1, maxLength);

    public static int Length(Random random, int minLength, int maxLength)
        => random.Next(minLength, maxLength);

    public static bool Boolean() => Boolean(System.Random.Shared);

    public static bool Boolean(Random random) => Int32(random, 0, 2) == 0;

    public static T Enum<T>()
        where T : Enum => Enum<T>(System.Random.Shared);

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

    public static T[] Array<T>(Func<T> generator)
    {
        var length = Length();
        var items = new T[length];
        for (var i = 0; i < length; i++)
        {
            items[i] = generator();
        }

        return items;
    }

    public static T[] Array<T>(Random random, Func<T> generator)
    {
        var length = Length(random);
        var items = new T[length];
        for (var i = 0; i < length; i++)
        {
            items[i] = generator();
        }

        return items;
    }

    public static List<T> List<T>(Func<T> generator)
    {
        var length = Length();
        var items = new T[length];
        for (var i = 0; i < length; i++)
        {
            items[i] = generator();
        }

        return [.. items];
    }

    public static List<T> List<T>(Random random, Func<T> generator)
    {
        var length = Length(random);
        var items = new T[length];
        for (var i = 0; i < length; i++)
        {
            items[i] = generator();
        }

        return [.. items];
    }

    public static ImmutableArray<T> ImmutableArray<T>(Func<T> generator)
    {
        var length = Length();
        var items = new T[length];
        for (var i = 0; i < length; i++)
        {
            items[i] = generator();
        }

        return System.Collections.Immutable.ImmutableArray.Create(items);
    }

    public static ImmutableArray<T> ImmutableArray<T>(Random random, Func<T> generator)
    {
        var length = Length(random);
        var items = new T[length];
        for (var i = 0; i < length; i++)
        {
            items[i] = generator();
        }

        return System.Collections.Immutable.ImmutableArray.Create(items);
    }

    public static ImmutableList<T> ImmutableList<T>(Func<T> generator)
    {
        var length = Length();
        var items = new T[length];
        for (var i = 0; i < length; i++)
        {
            items[i] = generator();
        }

        return System.Collections.Immutable.ImmutableList.Create(items);
    }

    public static ImmutableList<T> ImmutableList<T>(Random random, Func<T> generator)
    {
        var length = Length(random);
        var items = new T[length];
        for (var i = 0; i < length; i++)
        {
            items[i] = generator();
        }

        return System.Collections.Immutable.ImmutableList.Create(items);
    }

    public static T? RandomOrDefault<T>(this IEnumerable<T> enumerable)
        => RandomOrDefault(enumerable, item => true);

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

    public static T Random<T>(this IEnumerable<T> enumerable)
        => Random(enumerable, item => true);

    public static T Random<T>(this IEnumerable<T> enumerable, Func<T, bool> predicate)
    {
        if (enumerable.Any() is false)
        {
            throw new InvalidOperationException(
                "there is no random item that matches the condition.");
        }

        return RandomOrDefault(enumerable, predicate)!;
    }

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
