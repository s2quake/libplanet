using System.Reactive;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Schema;
using BitFaster.Caching;
using BitFaster.Caching.Lru;
using Libplanet.Extensions;
using Libplanet.Net.Messages;
using Libplanet.Net.Threading;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

// consensus.BlockProposed.Subscribe(consensus.Post);
//         consensus.PreVoted.Subscribe(consensus.Post);
//         consensus.PreCommitted.Subscribe(consensus.Post);
//         consensus.Completed.Subscribe(e =>
//         {
//             _ = Task.Run(() => blockchain.Append(e.Block, e.BlockCommit));
//         });

public interface IConsensusMediator
{
    // ISigner Signer { get; }

    // Block ProposeBlock();

    void Propose(Proposal proposal);

    void Vote(Vote vote);

    void Claim(ProposalClaim claim);

    void Quorum(Maj23 maj23);

}