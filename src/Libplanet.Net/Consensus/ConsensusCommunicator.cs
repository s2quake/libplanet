// using Libplanet.Net.Messages;

// namespace Libplanet.Net.Consensus;

// internal sealed class ConsensusCommunicator(Consensus consensus, Gossip gossip) : IDisposable
// {
//     private readonly IDisposable _preVoteSubscription = consensus.PreVote.Subscribe(vote
//         => gossip.PublishMessage(new ConsensusPreVoteMessage { PreVote = vote }));

//     private readonly IDisposable _preCommitSubscription = consensus.PreCommit.Subscribe(vote
//         => gossip.PublishMessage(new ConsensusPreCommitMessage { PreCommit = vote }));

//     private readonly IDisposable _quorumReachSubscription = consensus.QuorumReach.Subscribe(maj23
//         => gossip.PublishMessage(new ConsensusMaj23Message { Maj23 = maj23 }));

//     private readonly IDisposable _proposalClaimSubscription = consensus.ProposalClaim.Subscribe(proposalClaim
//         => gossip.PublishMessage(new ConsensusProposalClaimMessage { ProposalClaim = proposalClaim }));

//     private readonly IDisposable _blockProposeSubscription = consensus.BlockPropose.Subscribe(proposal
//         => gossip.PublishMessage(new ConsensusProposalMessage { Proposal = proposal }));

//     public void Dispose()
//     {
//         _preVoteSubscription.Dispose();
//         _preCommitSubscription.Dispose();
//         _quorumReachSubscription.Dispose();
//         _proposalClaimSubscription.Dispose();
//         _blockProposeSubscription.Dispose();
//     }
// }

