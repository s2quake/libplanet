// using System.ComponentModel;
// using System.Reflection;

// namespace Libplanet.Serialization.Json.Schema;

// public sealed class ModelSchemaBuilder
// {
// #pragma warning disable S1075 // URIs should not be hardcoded
//     // public const string BaseSchemaUrl = "https://json.schemastore.org/appsettings.json";
// #pragma warning restore S1075 // URIs should not be hardcoded

//     private readonly Dictionary<string, Type> _typeByName = [];

//     public static async Task<string> GetSchemaAsync(CancellationToken cancellationToken)
//     {
//         var schemaBuilder = new ModelSchemaBuilder();
//         var modelTypes = ServiceUtility.GetTypes(typeof(ModelAttribute), inherit: true);
//         foreach (var modelType in modelTypes)
//         {
//             var modelAttributes = GetAttributes(modelType);
//             foreach (var modelAttribute in modelAttributes)
//             {
//                 schemaBuilder.Add(modelAttribute.TypeName, modelType);
//             }

//             cancellationToken.ThrowIfCancellationRequested();
//         }

//         return await schemaBuilder.BuildAsync(cancellationToken);

//         static IEnumerable<ModelAttribute> GetAttributes(Type type)
//             => Attribute.GetCustomAttributes(type, typeof(ModelAttribute))
//                 .OfType<ModelAttribute>();
//     }

//     public void Add(string name, Type type)
//     {
//         _typeByName.Add(name, type);
//     }

//     public async Task<string> BuildAsync(CancellationToken cancellationToken)
//     {
//         var schema = new JsonSchema();
//         // var originSchema = await JsonSchema.FromUrlAsync(BaseSchemaUrl, cancellationToken);
//         var optionsSchema = new JsonSchema
//         {
//             Type = JsonObjectType.Object,
//         };

//         // schema.Definitions["appsettings"] = originSchema;
//         // schema.AllOf.Add(new JsonSchema
//         // {
//         //     Reference = originSchema,
//         // });
//         schema.AllOf.Add(optionsSchema);
//         foreach (var (name, type) in _typeByName)
//         {
//             var optionsType = type;
//             var settings = new SystemTextJsonSchemaGeneratorSettings
//             {
//                 ExcludedTypeNames = [optionsType.FullName!],
//                 FlattenInheritanceHierarchy = true,
//             };
//             var schemaGenerator = new ModelSchemaGenerator(settings);
//             var typeSchema = schemaGenerator.Generate(type);
//             schema.Definitions[name] = typeSchema;
//             optionsSchema.Properties.Add(name, new JsonSchemaProperty()
//             {
//                 Description = GetDescription(type),
//                 Reference = typeSchema,
//             });
//         }

//         return schema.ToJson();
//     }

//     private static string GetDescription(Type type)
//     {
//         if (type.GetCustomAttribute<DescriptionAttribute>() is { } descriptionAttribute)
//         {
//             return descriptionAttribute.Description;
//         }

//         return $"Type '{type.Name}' does not have a description.";
//     }
// }
