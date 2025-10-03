using System.IO;

namespace Libplanet.Serialization.Extensions;

public static class BinaryWriterExtensions
{
    public static void WriteEnum<T>(this BinaryWriter @this, T value)
        where T : Enum => WriteEnum(@this, value, typeof(T));

    public static void WriteEnum(this BinaryWriter @this, object value, Type enumType)
    {
        var underlyingType = Enum.GetUnderlyingType(enumType);
        if (underlyingType == typeof(long))
        {
            var bytes = BitConverter.GetBytes(Convert.ToInt64(value));
            @this.Write((byte)1);
            @this.Write(bytes, 0, bytes.Length);
        }
        else
        {
            var bytes = BitConverter.GetBytes(Convert.ToInt32(value));
            @this.Write((byte)0);
            @this.Write(bytes, 0, bytes.Length);
        }
    }
}
