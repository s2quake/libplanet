using System.IO;
using MessagePack;

namespace Libplanet.Serialization.Extensions;

public static class MessagePackWriterExtensions
{
    // public static void WriteInt32(this MessagePackWriter @this, int value)
    // {
    //     var bytes = BitConverter.GetBytes(value);
    //     @this.Write(bytes, 0, bytes.Length);
    // }

    public static void WriteEnum<T>(this MessagePackWriter @this, T value)
        where T : Enum => WriteEnum(@this, value, typeof(T));

    public static void WriteEnum(this MessagePackWriter @this, object value, Type enumType)
    {
        var underlyingType = Enum.GetUnderlyingType(enumType);
        if (underlyingType == typeof(long))
        {
            // var bytes = BitConverter.GetBytes(Convert.ToInt64(value));
            // @this.Write((byte)1);
            @this.Write(Convert.ToInt64(value));
        }
        else
        {
            // var bytes = BitConverter.GetBytes(Convert.ToInt32(value));
            // @this.Write((byte)0);
            @this.Write(Convert.ToInt32(value));
        }
    }

    // public static int ReadInt32(this MessagePackWriter @this)
    // {
    //     var bytes = new byte[sizeof(int)];
    //     if (@this.Read(bytes, 0, bytes.Length) != bytes.Length)
    //     {
    //         throw new EndOfMessagePackWriterException("Failed to read int32 from stream.");
    //     }

    //     return BitConverter.ToInt32(bytes);
    // }

    // public static T ReadEnum<T>(this MessagePackWriter @this)
    //     where T : Enum => (T)ReadEnum(@this, typeof(T));

    // public static object ReadEnum(this MessagePackWriter @this, Type enumType)
    // {
    //     var isLong = @this.ReadByte() == 1;
    //     var bytes = new byte[isLong ? sizeof(long) : sizeof(int)];
    //     if (@this.Read(bytes, 0, bytes.Length) != bytes.Length)
    //     {
    //         throw new EndOfMessagePackWriterException("Failed to read enum from stream.");
    //     }

    //     return isLong
    //         ? Enum.ToObject(enumType, BitConverter.ToInt64(bytes))
    //         : Enum.ToObject(enumType, BitConverter.ToInt32(bytes));
    // }
}
