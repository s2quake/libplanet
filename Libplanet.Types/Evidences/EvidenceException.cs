using System;
using System.Runtime.Serialization;

namespace Libplanet.Types.Evidences
{
    /// <summary>
    /// Serves as the base class for exceptions related <see cref="Evidence"/>s'
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

        public abstract Evidence CreateEvidence(IEvidenceContext evidenceContext);
    }
}