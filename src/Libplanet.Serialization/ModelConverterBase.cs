#pragma warning disable SA1402 // File may only contain a single type
using System.Diagnostics;
using System.IO;

namespace Libplanet.Serialization;

public abstract class ModelConverterBase : IModelConverter
{
    object IModelConverter.Deserialize(Stream stream, ModelOptions options)
        => Deserialize(stream, options);

    void IModelConverter.Serialize(object obj, Stream stream, ModelOptions options)
        => Serialize(obj, stream, options);

    protected abstract object Deserialize(Stream stream, ModelOptions options);

    protected abstract void Serialize(object obj, Stream stream, ModelOptions options);
}

public abstract class ModelConverterBase<T> : IModelConverter
    where T : notnull
{
    object IModelConverter.Deserialize(Stream stream, ModelOptions options)
        => Deserialize(stream, options);

    void IModelConverter.Serialize(object obj, Stream stream, ModelOptions options)
    {
        if (obj is T t)
        {
            Serialize(t, stream, options);
        }
        else
        {
            throw new UnreachableException("The object is not of the expected type.");
        }
    }

    protected abstract T Deserialize(Stream stream, ModelOptions options);

    protected abstract void Serialize(T obj, Stream stream, ModelOptions options);
}
