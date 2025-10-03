using System.ComponentModel.DataAnnotations;
using Libplanet.Serialization.DataAnnotations;

namespace Libplanet.Serialization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum)]
public sealed class ModelAttribute(string typeName) : Attribute
{
    [NotEmpty]
    public string TypeName { get; } = typeName;

    [NonNegative]
    public required int Version { get; init; }

    internal void Validate(Type modelType, int previousVersion, Type? previousType)
    {
        var validationContext = new ValidationContext(this);
        Validator.ValidateObject(this, validationContext, validateAllProperties: true);

        if (Version != previousVersion + 1)
        {
            throw new InvalidModelException($"The version of type '{modelType}' must be {previousVersion + 1}.", modelType);
        }

        if (previousType is not null)
        {
            if (modelType.GetConstructor([previousType]) is null)
            {
                var message = $"Type '{modelType}' does not have a constructor with a single parameter of type " +
                              $"'{previousType}'.";
                throw new InvalidModelException(message, modelType);
            }

            if (modelType.GetConstructor([]) is null)
            {
                throw new InvalidModelException($"Type '{modelType}' does not have a default constructor.", modelType);
            }
        }
    }
}
