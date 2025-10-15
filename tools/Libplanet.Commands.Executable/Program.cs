using JSSoft.Commands;
using JSSoft.Commands.Extensions;
using Libplanet.Commands;
using Libplanet.Commands.Blocks;
using Libplanet.Commands.Executable;
using Libplanet.Commands.Keys;
using Libplanet.Commands.Protocols;
using Libplanet.Commands.Repositories;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddSingleton<BlockCommand>()
    .AddSingleton<ICommand>(s => s.GetRequiredService<BlockCommand>());
services.AddSingleton<ICommand, GenesisBlockCommand>();
services.AddSingleton<ICommand, InspectBlockCommand>();

services.AddSingleton<KeyCommand>()
    .AddSingleton<ICommand>(s => s.GetRequiredService<KeyCommand>());
services.AddSingleton<ICommand, CreateKeyCommand>();
services.AddSingleton<ICommand, ListKeyCommand>();
services.AddSingleton<ICommand, RemoveKeyCommand>();
services.AddSingleton<ICommand, GenerateKeyCommand>();
services.AddSingleton<ICommand, DeriveKeyCommand>();
services.AddSingleton<ICommand, ImportKeyCommand>();
services.AddSingleton<ICommand, ExportKeyCommand>();
services.AddSingleton<ICommand, SignKeyCommand>();

services.AddSingleton<ProtocolCommand>()
    .AddSingleton<ICommand>(s => s.GetRequiredService<ProtocolCommand>());
services.AddSingleton<ICommand, SignProtocolCommand>();
services.AddSingleton<ICommand, InspectProtocolCommand>();

services.AddSingleton<RepositoryCommand>()
    .AddSingleton<ICommand>(s => s.GetRequiredService<RepositoryCommand>());
services.AddSingleton<ICommand, InitializeRepositoryCommand>();
services.AddSingleton<ICommand, InspectRepositoryCommand>();

var commandContext = new CommandContext(services.BuildServiceProvider());
await commandContext.ExecuteAsync(args);
