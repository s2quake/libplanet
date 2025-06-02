using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

namespace Libplanet.Serialization;

internal sealed record class ModelData
{
    public static readonly byte[] MagicValue = "LPNT"u8.ToArray();

    public string TypeName { get; init; } = string.Empty;

    public int Version { get; set; }

    public void Write(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(MagicValue);
        writer.Write(TypeName);
        writer.Write(Version);
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
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var bytes = new byte[MagicValue.Length];
            if (reader.Read(bytes) == bytes.Length && bytes.SequenceEqual(MagicValue))
            {
                data = new ModelData
                {
                    TypeName = reader.ReadString(),
                    Version = reader.ReadInt32(),
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
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var bytes = new byte[MagicValue.Length];
        if (reader.Read(bytes) != bytes.Length || !bytes.SequenceEqual(MagicValue))
        {
            throw new ModelSerializationException("Invalid magic value.");
        }

        return new ModelData
        {
            TypeName = reader.ReadString(),
            Version = reader.ReadInt32(),
        };
    }
}
