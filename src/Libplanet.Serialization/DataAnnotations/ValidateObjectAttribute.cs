using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Libplanet.Serialization.DataAnnotations;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ValidateObjectAttribute : ValidationAttribute
{
    public bool ValidateAllProperties { get; set; }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not null)
        {
            var serviceProvider = new ServiceProvider(validationContext);
            var items = validationContext.Items;
            var results = new List<ValidationResult>();
            var context = new ValidationContext(value, serviceProvider, items);
            var validateAllProperties = ValidateAllProperties;

            if (Validator.TryValidateObject(value, context, results, validateAllProperties))
            {
                return ValidationResult.Success;
            }

            var sb = new StringBuilder();
            sb.AppendLine(FormatErrorMessage(validationContext.DisplayName));
            foreach (var result in results)
            {
                var resultMemberNames = string.Join(", ", result.MemberNames);
                sb.AppendLine($"  - [{resultMemberNames}]: {result.ErrorMessage}");
            }

            var compositeMessage = sb.ToString();
            return new ValidationResult(compositeMessage, [validationContext.DisplayName]);
        }

        return base.IsValid(value, validationContext);
    }

    private sealed class ServiceProvider(ValidationContext validationContext) : IServiceProvider
    {
        object? IServiceProvider.GetService(Type serviceType)
            => validationContext.GetService(serviceType);
    }
}
