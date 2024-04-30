using System;
using System.Runtime.Serialization;
using Libplanet.Blockchain;
using Libplanet.Types.Consensus;

namespace Libplanet.Net.Consensus
{
    /// <summary>
    /// Serves as the base class for exceptions related <see cref="DuplicatedVoteEvidence"/>s'
    /// integrity and validity.
    /// </summary>
    public abstract class EvidenceException : Exception
    {
        /// <inheritdoc cref="Exception(string)"/>
        protected EvidenceException(string message)
            : base(message)
        {
        }

        /// <inheritdoc cref="Exception(string, Exception)"/>
        protected EvidenceException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <inheritdoc cref="Exception(SerializationInfo, StreamingContext)"/>
        protected EvidenceException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public abstract Evidence CreateEvidence(BlockChain blockChain);
    }
}
