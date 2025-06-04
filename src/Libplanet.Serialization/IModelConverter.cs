using System.IO;

namespace Libplanet.Serialization;

public interface IModelConverter
{
    Type Type { get; }

    void Serialize(object obj, Stream stream, ModelOptions options);

    object Deserialize(Stream stream, ModelOptions options);
}
