using System.Globalization;
using System.Numerics;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace Libplanet.Types;

public static class BencodexUtility
{
    private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffffffZ";
    private static readonly Codec _codec = new();

    public static byte[] Encode(IValue value) => _codec.Encode(value);

    public static IValue Decode(byte[] bytes) => _codec.Decode(bytes);

    public static IValue ToValue(IBencodable value) => value.Bencoded;

    public static IValue ToValue(int value) => new Integer(value);

    public static IValue ToValue(long value) => new Integer(value);

    public static IValue ToValue(BigInteger value) => new Integer(value);

    public static IValue ToValue(string value) => new Text(value);

    public static IValue ToValue(bool value) => new Bencodex.Types.Boolean(value);

    public static IValue ToValue(Enum @enum) => new Integer((int)(object)@enum);

    public static IValue ToValue(DateTimeOffset dateTimeOffset)
    {
        var text = dateTimeOffset.ToUniversalTime()
            .ToString(TimestampFormat, CultureInfo.InvariantCulture);
        return new Text(text);
    }

    public static IValue ToValue(Address? address) => address?.ToBencodex() ?? Null.Value;

    public static IValue ToValue(PublicKey? publicKey) => publicKey?.ToBencodex() ?? Null.Value;

    public static IValue ToValue(TxId? txId) => txId?.ToBencodex() ?? Null.Value;

    public static IValue ToValue(BlockHash? blockHash) => blockHash?.Bencoded ?? Null.Value;

    public static IValue ToValue<T>(ImmutableArray<T> values)
        where T : IBencodable
        => new List(values.Select(item => item.Bencoded));

    public static IValue ToValue<T>(ImmutableArray<T> values, Func<T, IValue> converter)
        => new List(values.Select(item => converter(item)));

    public static Address ToAddress(List list, int index) => Address.Create(list[index]);

    public static PublicKey ToPublicKey(List list, int index) => PublicKey.Create(list[index]);

    public static TxId ToTxId(List list, int index) => TxId.Create(list[index]);

    public static int ToInt32(List list, int index) => (int)(Integer)list[index];

    public static long ToInt64(List list, int index) => (long)(Integer)list[index];

    public static BigInteger ToBigInteger(List list, int index) => ((Integer)list[index]).Value;

    public static string GetString(List list, int index) => (Text)list[index];

    public static bool ToBoolean(List list, int index) => (Bencodex.Types.Boolean)list[index];

    public static DateTimeOffset ToDateTimeOffset(List list, int index)
        => DateTimeOffset.ParseExact(
            GetString(list, index), TimestampFormat, CultureInfo.InvariantCulture);

    public static BlockHash ToBlockHash(List list, int index) => new(list[index]);

    public static BlockHash? ToBlockHashOrDefault(List list, int index)
    {
        if (list[index] is Null)
        {
            return null;
        }

        return new BlockHash(list[index]);
    }

    public static T ToObject<T>(List list, int index)
        where T : IBencodable
        => ToObject(list, index, CreateInstance<T>);

    public static T ToObject<T>(List list, int index, Func<IValue, T> creator)
        where T : IBencodable
        => creator(list[index]);

    public static ImmutableArray<T> ToObjects<T>(List list, int index)
        where T : IBencodable
        => ToObjects(list, index, CreateInstance<T>);

    public static ImmutableArray<T> ToObjects<T>(List list, int index, Func<IValue, T> creator)
        => [.. ((List)list[index]).Select(creator)];

    public static ImmutableArray<T> ToObjects<T>(IValue value, Func<IValue, T> creator)
        => [.. ((List)value).Select(creator)];

    public static T ToEnum<T>(List list, int index)
        where T : Enum
        => (T)(object)(int)(Integer)list[index];

    private static T CreateInstance<T>(IValue value)
        where T : IBencodable
    {
        if (Activator.CreateInstance(typeof(T), value) is not T instance)
        {
            throw new InvalidCastException($"Failed to create an instance of {typeof(T)}.");
        }

        return instance;
    }
}
