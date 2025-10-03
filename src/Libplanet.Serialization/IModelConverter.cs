using System.IO;

namespace Libplanet.Serialization;

public interface IModelConverter
{
    bool CanConvert(Type type);

    void Write(BinaryWriter writer, object value, ModelOptions options);

    object? Read(BinaryReader reader, Type type, ModelOptions options);
}
