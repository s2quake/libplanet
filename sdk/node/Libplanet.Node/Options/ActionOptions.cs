namespace Libplanet.Node.Options;

[Options(Position)]
public sealed class ActionOptions : OptionsBase<ActionOptions>
{
    public const string Position = "Action";

    public string ModulePath { get; set; } = string.Empty;

    public string ActionProviderType { get; set; } = string.Empty;
}
