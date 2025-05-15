using System.Diagnostics.CodeAnalysis;
using System.IO;
using Libplanet.Serialization.Extensions;

namespace Libplanet.Serialization;

internal sealed record class ModelData
{
    public static readonly byte[] MagicValue = "LPNT"u8.ToArray();

    public string TypeName { get; init; } = string.Empty;

    public int Version { get; set; }

    public void Write(Stream stream)
    {
        stream.Write(MagicValue);
        stream.WriteString(TypeName);
        stream.WriteInt32(Version);
    }

    public static bool IsData(Stream stream)
    {
        var position = stream.Position;
        try
        {
            var bytes = new byte[MagicValue.Length];
            if (stream.Read(bytes) == bytes.Length && bytes.SequenceEqual(MagicValue))
            {
                return true;
            }

            return false;
        }
        finally
        {
            stream.Position = position;
        }
    }

    public static bool TryGetData(Stream stream, [MaybeNullWhen(false)] out ModelData data)
    {
        try
        {
            var bytes = new byte[MagicValue.Length];
            if (stream.Read(bytes) == bytes.Length && bytes.SequenceEqual(MagicValue))
            {
                data = new ModelData
                {
                    TypeName = stream.ReadString(),
                    Version = stream.ReadInt32(),
                };
                return true;
            }
        }
        catch
        {
            // Ignore exceptions
        }

        data = default;
        return false;
    }

    public static ModelData GetData(Stream stream)
    {
        var bytes = new byte[MagicValue.Length];
        if (stream.Read(bytes) != bytes.Length || !bytes.SequenceEqual(MagicValue))
        {
            throw new ModelSerializationException("Invalid magic value.");
        }

        return new ModelData
        {
            TypeName = stream.ReadString(),
            Version = stream.ReadInt32(),
        };
    }

    // internal static IValue GetValue(IValue value, string typeName)
    // {
    //     try
    //     {
    //         var data = GetData(value);
    //         if (typeName != data.Header.TypeName)
    //         {
    //             throw new ModelSerializationException(
    //                 $"Given type name {data.Header.TypeName} is not {typeName}");
    //         }

    //         return data.Value;
    //     }
    //     catch (ModelSerializationException)
    //     {
    //         throw;
    //     }
    //     catch (Exception e)
    //     {
    //         throw new ModelSerializationException(e.Message, e);
    //     }
    // }
}
