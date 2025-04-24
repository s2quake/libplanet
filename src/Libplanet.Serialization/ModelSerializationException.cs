namespace Libplanet.Serialization;

public class ModelSerializationException : Exception
{
    public ModelSerializationException(string message)
        : base(message)
    {
    }

    public ModelSerializationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
