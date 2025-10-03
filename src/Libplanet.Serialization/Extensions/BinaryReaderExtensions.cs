using System.IO;

namespace Libplanet.Serialization.Extensions;

public static class BinaryReaderExtensions
{
    public static T ReadEnum<T>(this BinaryReader @this)
        where T : Enum => (T)ReadEnum(@this, typeof(T));

    public static object ReadEnum(this BinaryReader @this, Type enumType)
    {
        var isLong = @this.ReadByte() == 1;
        var bytes = new byte[isLong ? sizeof(long) : sizeof(int)];
        if (@this.Read(bytes, 0, bytes.Length) != bytes.Length)
        {
            throw new EndOfStreamException("Failed to read enum from stream.");
        }

        return isLong
            ? Enum.ToObject(enumType, BitConverter.ToInt64(bytes))
            : Enum.ToObject(enumType, BitConverter.ToInt32(bytes));
    }
}
