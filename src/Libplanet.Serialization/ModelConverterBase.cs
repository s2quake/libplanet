#pragma warning disable SA1402 // File may only contain a single type
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Libplanet.Serialization;

public abstract class ModelConverterBase(Type Type) : IModelConverter
{
    public Type Type { get; } = Type;

    void IModelConverter.Serialize(object obj, ref ModelWriter writer, ModelOptions options)
        => Serialize(obj, ref writer, options);

    object IModelConverter.Deserialize(ref ModelReader reader, ModelOptions options)
        => Deserialize(ref reader, options);

    protected abstract void Serialize(object obj, ref ModelWriter writer, ModelOptions options);

    protected abstract object Deserialize(ref ModelReader reader, ModelOptions options);
}

public abstract class ModelConverterBase<T> : IModelConverter
    where T : notnull
{
    public Type Type { get; } = typeof(T);

    void IModelConverter.Serialize(object obj, ref ModelWriter writer, ModelOptions options)
    {
        if (obj is T t)
        {
            Serialize(t, ref writer, options);
        }
        else
        {
            throw new UnreachableException("The object is not of the expected type.");
        }
    }

    object IModelConverter.Deserialize(ref ModelReader reader, ModelOptions options)
    {
        return Deserialize(ref reader, options);
    }

    protected abstract void Serialize(T obj, ref ModelWriter writer, ModelOptions options);

    protected abstract T Deserialize(ref ModelReader reader, ModelOptions options);
}
