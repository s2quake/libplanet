using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus
{
    /// <summary>
    /// Interface for communicating <see cref="ConsensusMessage"/>s with peers.
    /// </summary>
    public interface IConsensusMessageCommunicator
    {
        /// <summary>
        /// Publish given <paramref name="message"/> to peers.
        /// </summary>
        /// <param name="message"><see cref="ConsensusMessage"/> to publish.</param>
        public void PublishMessage(ConsensusMessage message);

        /// <summary>
        /// Method that will be called on the
        /// <see cref="Context.Start"/> call.
        /// </summary>
        /// <param name="height"><see cref="Context.Height"/>
        /// to trigger this method.</param>
        public void OnStartHeight(int height);

        /// <summary>
        /// Method that will be called on the
        /// <see cref="Context.StartRound(int)"/> call.
        /// </summary>
        /// <param name="round"><see cref="Context.Round"/>
        /// to trigger this method.</param>
        public void OnStartRound(int round);
    }
}
