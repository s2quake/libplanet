using System.IO;

namespace Libplanet.Serialization;

public interface IModelConverter
{
    Type Type { get; }

    void Serialize(object obj, ref ModelWriter writer, ModelOptions options);

    object Deserialize(ref ModelReader reader, ModelOptions options);
}
