namespace Libplanet.Serialization;

public class InvalidModelException : ModelException
{
    public InvalidModelException(string message, Type modelType)
        : base(message)
    {
        ModelType = modelType;
    }

    public InvalidModelException(string message, Type modelType, Exception innerException)
        : base(message, innerException)
    {
        ModelType = modelType;
    }

    public Type ModelType { get; }
}
