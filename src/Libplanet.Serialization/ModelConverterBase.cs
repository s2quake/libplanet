#pragma warning disable SA1402 // File may only contain a single type
using System.Diagnostics;
using System.IO;

namespace Libplanet.Serialization;

public abstract class ModelConverterBase : IModelConverter
{
    object IModelConverter.Deserialize(Stream stream) => Deserialize(stream);

    void IModelConverter.Serialize(object obj, Stream stream) => Serialize(obj, stream);

    protected abstract object Deserialize(Stream stream);

    protected abstract void Serialize(object obj, Stream stream);
}

public abstract class ModelConverterBase<T> : IModelConverter
    where T : notnull
{
    object IModelConverter.Deserialize(Stream stream) => Deserialize(stream);

    void IModelConverter.Serialize(object obj, Stream stream)
    {
        if (obj is T t)
        {
            Serialize(t, stream);
        }
        else
        {
            throw new UnreachableException("The object is not of the expected type.");
        }
    }

    protected abstract T Deserialize(Stream stream);

    protected abstract void Serialize(T obj, Stream stream);
}
