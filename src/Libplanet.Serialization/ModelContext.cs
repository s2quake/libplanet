namespace Libplanet.Serialization;

public record class ModelContext : IServiceProvider
{
    private readonly IServiceProvider? _serviceProvider;

    public ModelContext()
    {
    }

    public ModelContext(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ModelContext(IDictionary<object, object> items)
    {
        Items = items;
    }

    public static ModelContext Empty { get; } = new();

    public IDictionary<object, object> Items { get; } = new Dictionary<object, object>();

    public object? GetService(Type serviceType) => _serviceProvider?.GetService(serviceType);
}
