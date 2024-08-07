using Libplanet.Node.API.Services;
using Libplanet.Node.Extensions;
using Libplanet.Node.Options;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using NJsonSchema;
using NJsonSchema.Generation;

SynchronizationContext.SetSynchronizationContext(new());
var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();

if (builder.Environment.IsDevelopment())
{
    builder.WebHost.ConfigureKestrel(options =>
    {
        // Setup a HTTP/2 endpoint without TLS.
        options.ListenLocalhost(5259, o => o.Protocols =
            HttpProtocols.Http2);
    });
}

var settings = new SystemTextJsonSchemaGeneratorSettings
{
    GenerateEnumMappingDescription = false,
};
var schema = JsonSchema.FromType<LibplanetOption>(settings);
var schemaData = schema.ToJson();

File.WriteAllText("/Users/jeesu/Projects/s2quake/libplanet/sdk/node/Libplanet.Node.Executable/libplanet-options-schema.json", schemaData);

// Additional configuration is required to successfully run gRPC on macOS.
// For instructions on how to configure Kestrel and gRPC clients on macOS,
// visit https://go.microsoft.com/fwlink/?linkid=2099682

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();
builder.Services.AddLibplanetNode(builder.Configuration.GetSection("Libplanet"))
    .WithSwarm();

var app = builder.Build();
var handlerMessage = """
    Communication with gRPC endpoints must be made through a gRPC client. To learn how to
    create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909
    """;

// Configure the HTTP request pipeline.
app.MapGrpcService<GreeterService>();
app.MapGet("/", () => handlerMessage);

if (builder.Environment.IsDevelopment())
{
    app.MapGrpcReflectionService().AllowAnonymous();
}

await app.RunAsync();
