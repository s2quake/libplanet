// using System.ComponentModel.DataAnnotations;

// namespace Libplanet.Serialization.DataAnnotations;

// public sealed class ValidationScope : IDisposable
// {
//     private static readonly HashSet<IWorldContext> _contextSet = [];
//     private readonly IWorldContext _worldContext;

//     public ValidationScope(IWorldContext worldContext)
//     {
//         if (worldContext.IsReadOnly)
//         {
//             throw new InvalidOperationException(
//                 "Validation scope cannot be enabled in read-only context.");
//         }

//         if (_contextSet.Contains(worldContext))
//         {
//             throw new InvalidOperationException("Validation scope is already enabled.");
//         }

//         _contextSet.Add(worldContext);
//         _worldContext = worldContext;
//     }

//     public void Dispose()
//     {
//         _contextSet.Remove(_worldContext);
//     }

//     internal static void Validate(IWorldContext worldContext, object value)
//     {
//         if (_contextSet.Contains(worldContext))
//         {
//             var context = new ValidationContext(value);
//             Validator.ValidateObject(value, context, validateAllProperties: true);
//         }
//     }
// }
