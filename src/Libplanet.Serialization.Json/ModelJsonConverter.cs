using System.Text.Json.Serialization;

namespace Libplanet.Serialization.Json;

public abstract class ModelJsonConverter<T>(ModelOptions modelOptions) : JsonConverter<T>
{
    protected ModelJsonConverter()
        : this(ModelOptions.Empty)
    {
    }

    protected ModelOptions ModelOptions { get; } = modelOptions;
}
