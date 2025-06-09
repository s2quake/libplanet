using System.ComponentModel.DataAnnotations;
using Libplanet.Serialization.DataAnnotations;

namespace Libplanet.Serialization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class ModelAttribute : Attribute
{
    [NonNegative]
    public required int Version { get; init; }

    [NotEmpty]
    public required string TypeName { get; init; }

    internal void Validate(Type modelType, int previousVersion, Type? previousType)
    {
        var validationContext = new ValidationContext(this);
        Validator.ValidateObject(this, validationContext, validateAllProperties: true);

        if (Version != previousVersion + 1)
        {
            throw new ArgumentException(
                $"Version of {modelType} must be sequential starting from 1", nameof(modelType));
        }

        if (previousType is not null)
        {
            if (modelType.GetConstructor([previousType]) is null)
            {
                throw new ArgumentException(
                    $"Type {modelType} does not have a constructor with {previousType}", nameof(modelType));
            }

            if (modelType.GetConstructor([]) is null)
            {
                throw new ArgumentException(
                    $"Type {modelType} does not have a default constructor", nameof(modelType));
            }
        }
    }
}
