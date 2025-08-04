using GraphQL.Types;
using Libplanet.Explorer.Interfaces;
using Libplanet.Net;

namespace Libplanet.Explorer.GraphTypes
{
    public class NodeStateType : ObjectGraphType<IBlockChainContext>
    {
        public NodeStateType()
        {
            Name = "NodeState";

            // Field<NonNullGraphType<BoundPeerType>>(
            //     name: "self",
            //     resolve: context => context.Source.Swarm.Peer);
            // Field<NonNullGraphType<BooleanGraphType>>(
            //     name: "preloaded",
            //     resolve: context => context.Source.Preloaded);
            // Field<NonNullGraphType<ListGraphType<NonNullGraphType<BoundPeerType>>>>(
            //     name: "peers",
            //     resolve: context => context.Source.Swarm?.Peers ?? new List<Peer>());
            // Field<NonNullGraphType<ListGraphType<NonNullGraphType<BoundPeerType>>>>(
            //     name: "validators",
            //     resolve: context => context.Source.Swarm?.Validators ?? []);
        }
    }
}
