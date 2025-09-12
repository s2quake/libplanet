// using System.Diagnostics;

// namespace Libplanet.Serialization.Descriptors;

// internal sealed class KeyValuePairModelDescriptor : ModelDescriptor
// {
//     public override bool CanSerialize(Type type) => IsKeyValuePair(type);

//     public override Type[] GetTypes(Type type, out bool isArray)
//     {
//         isArray = false;
//         var genericArguments = type.GetGenericArguments();
//         if (genericArguments.Length != 2)
//         {
//             throw new ModelSerializationException(
//                 $"The number of generic arguments {genericArguments.Length} does not match " +
//                 $"the number of tuple items 2");
//         }

//         return [genericArguments[0], genericArguments[1]];
//     }

//     public override object?[] Serialize(object obj, Type type, ModelOptions options)
//     {
//         if (type.GetProperty(nameof(KeyValuePair<string, string>.Key)) is not { } keyProperty)
//         {
//             throw new UnreachableException("Key property not found");
//         }

//         if (type.GetProperty(nameof(KeyValuePair<string, string>.Value)) is not { } valueProperty)
//         {
//             throw new UnreachableException("Value property not found");
//         }

//         return [keyProperty.GetValue(obj), valueProperty.GetValue(obj)];
//     }

//     public override object Deserialize(Type type, object?[] values, ModelOptions options)
//         => TypeUtility.CreateInstance(type, args: [values[0], values[1]]);

//     public override bool Equals(object obj1, object obj2, Type type)
//     {
//         var propertyInfos = type.GetProperties();
//         foreach (var propertyInfo in propertyInfos)
//         {
//             var value1 = propertyInfo.GetValue(obj1);
//             var value2 = propertyInfo.GetValue(obj2);
//             if (!ModelResolver.Equals(value1, value2, propertyInfo.PropertyType))
//             {
//                 return false;
//             }
//         }

//         return true;
//     }

//     public override int GetHashCode(object obj, Type type)
//     {
//         var propertyInfos = type.GetProperties();
//         HashCode hash = default;
//         foreach (var propertyInfo in propertyInfos)
//         {
//             var value = propertyInfo.GetValue(obj);
//             hash.Add(ModelResolver.GetHashCode(value, propertyInfo.PropertyType));
//         }

//         return hash.ToHashCode();
//     }

//     private static bool IsKeyValuePair(Type type)
//     {
//         if (!type.IsGenericType)
//         {
//             return false;
//         }

//         var genericTypeDefinition = type.GetGenericTypeDefinition();
//         return genericTypeDefinition == typeof(KeyValuePair<,>);
//     }
// }
