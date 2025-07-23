using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.NetMQ;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Tests.Store;
using Libplanet.Types;
using Nito.AsyncEx;
using Serilog;
using xRetry;
using Xunit.Abstractions;
using Libplanet.Extensions;
using static Libplanet.Tests.TestUtils;
using Libplanet.TestUtilities;
using Libplanet.Types.Threading;

namespace Libplanet.Net.Tests;

public static class SwarmExtensions
{
    public static Task AddPeersAsync(this Swarm @this, ImmutableArray<Peer> peers, CancellationToken cancellationToken)
        => @this.PeerService.AddPeersAsync(peers, cancellationToken);
}
