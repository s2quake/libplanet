using System.IO;

namespace Libplanet.Serialization;

public interface IModelConverter
{
    void Serialize(object obj, Stream stream);

    object Deserialize(Stream stream);
}
