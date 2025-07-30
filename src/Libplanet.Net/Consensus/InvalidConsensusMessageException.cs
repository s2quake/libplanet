using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus
{
    /// <summary>
    /// An exception thrown when a received <see cref="ConsensusMessage"/> is invalid.  In particular,
    /// this is thrown pre-emptively before a <see cref="ConsensusMessage"/> is processed, i.e.
    /// does not change the state of a <see cref="Context"/> in a meaningful way.
    /// </summary>
    public class InvalidConsensusMessageException : InvalidMessageContentException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidConsensusMessageException"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.
        /// </param>
        /// <param name="receivedMessage">The <see cref="ConsensusMessage"/> that caused this exception.
        /// </param>
        /// <param name="innerException">The exception that is the cause of the current exception.
        /// </param>
        public InvalidConsensusMessageException(
            string message,
            MessageBase receivedMessage,
            Exception innerException)
            : base(message, receivedMessage, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidConsensusMessageException"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.
        /// </param>
        /// <param name="receivedMessage">The <see cref="ConsensusMessage"/> that caused this exception.
        /// </param>
        public InvalidConsensusMessageException(string message, MessageBase receivedMessage)
            : base(message, receivedMessage)
        {
        }
    }
}
