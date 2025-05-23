namespace Libplanet;

public sealed class RelayValidator<T>(Action<T> validator) : IValidator<T>
{
    public RelayValidator()
        : this(_ => { })
    {
    }

    void IValidator<T>.Validate(T obj) => validator(obj);
}
