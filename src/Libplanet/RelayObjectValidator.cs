namespace Libplanet;

public sealed class RelayObjectValidator<T>(Action<T> validator) : IObjectValidator<T>
{
    public RelayObjectValidator()
        : this(_ => { })
    {
    }

    void IObjectValidator<T>.Validate(T obj) => validator(obj);
}
