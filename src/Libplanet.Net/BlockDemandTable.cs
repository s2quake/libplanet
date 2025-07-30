using System.Collections.Concurrent;
using Libplanet.Blockchain;
using Serilog;

namespace Libplanet.Net
{
    public class BlockDemandTable
    {
        private readonly TimeSpan _blockDemandLifespan;
        private readonly ConcurrentDictionary<BoundPeer, BlockDemand> _blockDemands;

        public BlockDemandTable(TimeSpan blockDemandLifespan)
        {
            _blockDemandLifespan = blockDemandLifespan;
            _blockDemands = new ConcurrentDictionary<BoundPeer, BlockDemand>();
        }

        public IDictionary<BoundPeer, BlockDemand> Demands =>
            _blockDemands.ToDictionary(pair => pair.Key, pair => pair.Value);

        public bool Any() => _blockDemands.Any();

        public void Add(
            BlockChain blockChain,
            Func<BlockExcerpt, bool> predicate,
            BlockDemand demand)
        {
            if (IsDemandNeeded(blockChain, predicate, demand))
            {
                _blockDemands[demand.Peer] = demand;
                Log.Debug(
                    "BlockDemand #{Index} {BlockHash} from peer {Peer} updated",
                    demand.Index,
                    demand.Hash,
                    demand.Peer);
            }
            else
            {
                Log.Debug(
                    "BlockDemand #{Index} {BlockHash} from peer {Peer} ignored",
                    demand.Index,
                    demand.Hash,
                    demand.Peer);
            }
        }

        public void Remove(BoundPeer peer)
        {
            _blockDemands.TryRemove(peer, out _);
        }

        public void Cleanup(
            BlockChain blockChain,
            Func<BlockExcerpt, bool> predicate)
        {
            foreach (var demand in _blockDemands.Values)
            {
                if (!predicate(demand) || IsDemandStale(demand))
                {
                    _blockDemands.TryRemove(demand.Peer, out _);
                }
            }

            Log.Verbose("Successfully cleaned up demands");
        }

        private bool IsDemandNeeded(
            BlockChain blockChain,
            Func<BlockExcerpt, bool> predicate,
            BlockDemand demand)
        {
            BlockDemand? oldDemand = _blockDemands.ContainsKey(demand.Peer)
                ? _blockDemands[demand.Peer]
                : (BlockDemand?)null;

            bool needed;
            if (IsDemandStale(demand))
            {
                needed = false;
            }
            else if (predicate(demand))
            {
                if (oldDemand is { } old)
                {
                    needed = IsDemandStale(old) || old.Index < demand.Index;
                }
                else
                {
                    needed = true;
                }
            }
            else
            {
                needed = false;
            }

            Log.Verbose(
                "Determining if a demand is actually needed: {Need}\n" +
                "Peer: {Peer}\n" +
                "Demand: {Demand}\n" +
                "Tip: {Tip}\n" +
                "Old Demand: {OldDemand}",
                needed,
                demand.Peer,
                demand.ToExcerptString(),
                blockChain.Tip.ToExcerptString(),
                oldDemand?.ToExcerptString());
            return needed;
        }

        private bool IsDemandStale(BlockDemand demand)
        {
            return demand.Timestamp + _blockDemandLifespan < DateTimeOffset.UtcNow;
        }
    }
}
