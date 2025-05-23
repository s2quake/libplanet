namespace Libplanet;

public interface IValidator<in T>
{
    void Validate(T obj);
}
