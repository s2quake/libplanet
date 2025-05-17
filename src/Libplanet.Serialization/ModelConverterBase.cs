#pragma warning disable SA1402 // File may only contain a single type
using System.Diagnostics;
using System.IO;

namespace Libplanet.Serialization;

public abstract class ModelConverterBase : IModelConverter
{
    object IModelConverter.Deserialize(Stream stream, ModelContext context)
        => Deserialize(stream, context);

    void IModelConverter.Serialize(object obj, Stream stream, ModelContext context)
        => Serialize(obj, stream, context);

    protected abstract object Deserialize(Stream stream, ModelContext context);

    protected abstract void Serialize(object obj, Stream stream, ModelContext context);
}

public abstract class ModelConverterBase<T> : IModelConverter
    where T : notnull
{
    object IModelConverter.Deserialize(Stream stream, ModelContext context)
        => Deserialize(stream, context);

    void IModelConverter.Serialize(object obj, Stream stream, ModelContext context)
    {
        if (obj is T t)
        {
            Serialize(t, stream, context);
        }
        else
        {
            throw new UnreachableException("The object is not of the expected type.");
        }
    }

    protected abstract T Deserialize(Stream stream, ModelContext context);

    protected abstract void Serialize(T obj, Stream stream, ModelContext context);
}
