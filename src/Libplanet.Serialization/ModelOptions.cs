namespace Libplanet.Serialization;

public sealed record class ModelOptions : IServiceProvider
{
    private readonly IServiceProvider? _serviceProvider;

    public ModelOptions()
    {
    }

    public ModelOptions(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public static ModelOptions Empty { get; } = new();

    public ImmutableDictionary<object, object?> Items { get; init; } = ImmutableDictionary<object, object?>.Empty;

    public bool IsValidationEnabled { get; init; }

    public object? GetService(Type serviceType) => _serviceProvider?.GetService(serviceType);
}
