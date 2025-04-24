namespace Libplanet.Serialization;

public sealed class ModelCreationException : ModelSerializationException
{
    public ModelCreationException(Type type)
        : base(GetMessage(type))
    {
    }

    public ModelCreationException(Type type, Exception innerException)
        : base(GetMessage(type), innerException)
    {
    }

    private static string GetMessage(Type type)
        => $"Failed to create model for type '{type.FullName}'.";
}
