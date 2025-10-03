using System.IO;

namespace Libplanet.Serialization;

internal sealed record class ModelData
{
    public ModelData()
    {
    }

    public ModelData(Type type)
    {
        (TypeName, Version) = ModelResolver.GetTypeInfo(type);
    }

    public string TypeName { get; init; } = string.Empty;

    public int Version { get; set; }

    public void Write(BinaryWriter writer)
    {
        writer.Write(TypeName);
        writer.Write7BitEncodedInt(Version);
    }

    public static ModelData GetData(BinaryReader reader) => new()
    {
        TypeName = reader.ReadString(),
        Version = reader.Read7BitEncodedInt(),
    };
}
