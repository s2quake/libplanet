using System.IO;
using System.Text;
using System.Text.Json;
using GraphQL;
using GraphQL.Types;
using Libplanet.Types;

namespace Libplanet.Explorer.GraphTypes
{
    public class ActionType : ObjectGraphType<byte[]>
    {
        public ActionType()
        {
            Name = "Action";

            Field<NonNullGraphType<StringGraphType>>(
                name: "Raw",
                description: "Raw Action data ('hex' or 'base64' encoding available.)",
                arguments: new QueryArguments(
                    new QueryArgument<StringGraphType>
                    {
                        DefaultValue = "hex",
                        Name = "encode",
                    }),
                resolve: ctx =>
                {
                    var encoded = ctx.Source;

                    var encode = ctx.GetArgument<string>("encode");
                    switch (encode)
                    {
                        case "hex":
                            return ByteUtility.Hex(encoded);

                        case "base64":
                            return Convert.ToBase64String(encoded);

                        default:
                            var msg =
                                "Unsupported 'encode' method came. " +
                                "It supports only 'hex' or 'base64'.";
                            throw new ExecutionError(msg);
                    }
                });

            Field<NonNullGraphType<StringGraphType>>(
                name: "Inspection",
                description: "A readable representation for debugging.",
                resolve: ctx => ctx.Source);

            Field<NonNullGraphType<StringGraphType>>(
                name: "json",
                description: "A JSON representation of action data",
                resolve: ctx =>
                {
                    throw new NotImplementedException();
                    // var converter = new Bencodex.Json.BencodexJsonConverter();
                    // var buffer = new MemoryStream();
                    // var writer = new Utf8JsonWriter(buffer);
                    // converter.Write(writer, ctx.Source, new JsonSerializerOptions());
                    // return Encoding.UTF8.GetString(buffer.ToArray());
                });
        }
    }
}
