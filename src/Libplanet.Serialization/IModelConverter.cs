using System.IO;

namespace Libplanet.Serialization;

public interface IModelConverter
{
    void Serialize(object obj, Stream stream, ModelOptions options);

    object Deserialize(Stream stream, ModelOptions options);
}
