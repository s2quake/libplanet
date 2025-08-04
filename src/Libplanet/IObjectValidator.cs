namespace Libplanet;

public interface IObjectValidator<in T>
{
    void Validate(T obj);
}
