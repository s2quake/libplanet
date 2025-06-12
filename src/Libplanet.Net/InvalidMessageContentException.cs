using Libplanet.Net.Messages;

namespace Libplanet.Net;

public class InvalidMessageContentException : Exception
{
    internal InvalidMessageContentException(
        string message,
        IMessage content,
        Exception innerException)
        : base(message, innerException)
    {
        Content = content;
    }

    internal InvalidMessageContentException(string message, IMessage content)
        : base(message)
    {
        Content = content;
    }

    internal IMessage Content { get; }
}
