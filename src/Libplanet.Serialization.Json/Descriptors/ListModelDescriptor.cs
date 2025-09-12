// using System.Collections;
// using System.Diagnostics;

// namespace Libplanet.Serialization.Descriptors;

// internal sealed class ListModelDescriptor : ModelDescriptor
// {
//     public override bool CanSerialize(Type type) => IsList(type);

//     public override Type[] GetTypes(Type type, out bool isArray)
//     {
//         isArray = true;
//         return [GetElementType(type)];
//     }

//     public override object?[] Serialize(object obj, Type type, ModelOptions options)
//     {
//         if (obj is IList items)
//         {
//             var values = new object?[items.Count];
//             for (var i = 0; i < items.Count; i++)
//             {
//                 values[i] = items[i];
//             }

//             return values;
//         }
//         else
//         {
//             throw new InvalidOperationException($"Cannot get values from {obj.GetType()}");
//         }
//     }

//     public override object Deserialize(Type type, object?[] values, ModelOptions options)
//     {
//         var elementType = GetElementType(type);
//         var listType = typeof(List<>).MakeGenericType(elementType);
//         var listInstance = (IList)TypeUtility.CreateInstance(listType, args: [values.Length])!;
//         foreach (var value in values)
//         {
//             listInstance.Add(value);
//         }

//         return listInstance;
//     }

//     public override bool Equals(object obj1, object obj2, Type type)
//     {
//         var list1 = (IList)obj1;
//         var list2 = (IList)obj2;
//         if (list1.GetType() != list2.GetType())
//         {
//             return false;
//         }

//         if (list1.Count != list2.Count)
//         {
//             return false;
//         }

//         var elementType = GetElementType(type);
//         for (var i = 0; i < list1.Count; i++)
//         {
//             if (!ModelResolver.Equals(list1[i], list2[i], elementType))
//             {
//                 return false;
//             }
//         }

//         return true;
//     }

//     public override int GetHashCode(object obj, Type type)
//     {
//         var items = (IList)obj;
//         var elementType = GetElementType(type);
//         HashCode hash = default;
//         foreach (var item in items)
//         {
//             hash.Add(ModelResolver.GetHashCode(item, elementType));
//         }

//         return hash.ToHashCode();
//     }

//     private static bool IsList(Type type)
//     {
//         if (type.IsGenericType)
//         {
//             var genericTypeDefinition = type.GetGenericTypeDefinition();
//             if (genericTypeDefinition == typeof(List<>))
//             {
//                 return true;
//             }
//         }

//         return false;
//     }

//     private static Type GetElementType(Type type)
//     {
//         if (type.IsGenericType)
//         {
//             var genericTypeDefinition = type.GetGenericTypeDefinition();
//             if (genericTypeDefinition == typeof(List<>))
//             {
//                 return type.GetGenericArguments()[0];
//             }
//         }

//         throw new UnreachableException("The type is not an ImmutableArray.");
//     }
// }
