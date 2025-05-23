namespace Libplanet.State;

public sealed record class ActionResult
{
    public required IAction Action { get; init; }

    public required IActionContext InputContext { get; init; }

    public required World InputWorld { get; init; }

    public required World OutputWorld { get; init; }

    public Exception? Exception { get; init; }

    public string ExceptionMessage
    {
        get
        {
            if (Exception is null)
            {
                return string.Empty;
            }

            if (Exception.InnerException is { } innerException)
            {
                return innerException.Message;
            }

            return Exception.Message;
        }
    }
}
