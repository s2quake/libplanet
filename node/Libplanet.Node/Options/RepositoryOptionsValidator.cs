namespace Libplanet.Node.Options;

internal sealed class RepositoryOptionsValidator
    : OptionsValidatorBase<RepositoryOptions>
{
    protected override void OnValidate(string? name, RepositoryOptions options)
    {
        if (options.Type is not RepositoryType.Memory)
        {
            if (options.Path == string.Empty)
            {
                throw new ArgumentException(
                    "RootPath must be set when StoreType is not Memory.");
            }

            _ = Path.GetFullPath(options.Path);
        }
    }
}
