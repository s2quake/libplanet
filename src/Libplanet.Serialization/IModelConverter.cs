using System.IO;

namespace Libplanet.Serialization;

public interface IModelConverter
{
    void Serialize(object obj, Stream stream, ModelContext context);

    object Deserialize(Stream stream, ModelContext context);
}
