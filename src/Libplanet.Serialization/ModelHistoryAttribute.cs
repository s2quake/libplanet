using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Libplanet.Serialization.DataAnnotations;

namespace Libplanet.Serialization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class ModelHistoryAttribute : Attribute
{
    [NonNegative]
    public required int Version { get; init; }

    public required Type Type { get; init; }

    internal void Validate(Type modelType, int previousVersion, Type? previousType)
    {
        var validationContext = new ValidationContext(this);
        Validator.ValidateObject(this, validationContext, validateAllProperties: true);
        var version = Version;
        if (version != previousVersion + 1)
        {
            throw new ArgumentException(
                $"Version of {modelType} must be sequential starting from 1", nameof(modelType));
        }

        if (Type.GetCustomAttribute<OriginModelAttribute>() is not { } originModelAttribute)
        {
            throw new ArgumentException(
                $"Type {Type} does not have {nameof(OriginModelAttribute)}",
                nameof(modelType));
        }

        if (originModelAttribute.Type != modelType)
        {
            throw new ArgumentException("OriginType of OriginModelAttribute is not valid", nameof(modelType));
        }

        if (previousType is not null)
        {
            if (Type.GetConstructor([previousType]) is null)
            {
                throw new ArgumentException(
                    $"Type {Type} does not have a constructor with {previousType}", nameof(modelType));
            }

            if (Type.GetConstructor([]) is null)
            {
                throw new ArgumentException(
                    $"Type {Type} does not have a default constructor", nameof(modelType));
            }
        }
    }
}
