using JSSoft.Commands;
using JSSoft.Commands.Extensions;
using Libplanet.Commands;
using Libplanet.Commands.Executable;
using Libplanet.Commands.Keys;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddSingleton<BlockCommand>()
    .AddSingleton<ICommand>(s => s.GetRequiredService<BlockCommand>());
services.AddSingleton<KeyCommand>()
    .AddSingleton<ICommand>(s => s.GetRequiredService<KeyCommand>());
services.AddSingleton<ICommand, CreateKeyCommand>();
services.AddSingleton<ICommand, ListKeyCommand>();
services.AddSingleton<ICommand, RemoveKeyCommand>();
services.AddSingleton<ICommand, GenerateKeyCommand>();
services.AddSingleton<ICommand, DeriveKeyCommand>();
services.AddSingleton<ICommand, ImportKeyCommand>();
services.AddSingleton<ICommand, ExportKeyCommand>();

var commandContext = new CommandContext(services.BuildServiceProvider());
await commandContext.ExecuteAsync(args);
