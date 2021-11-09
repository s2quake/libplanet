#nullable enable
using System;
using System.Runtime.Serialization;
using Libplanet.Blockchain.Policies;

namespace Libplanet.Blocks
{
    /// <summary>
    /// An exception thrown when <see cref="Block{T}.BytesLength"/>
    /// does not follow the constraint provided by <see cref="IBlockPolicy{T}"/>.
    /// </summary>
    [Serializable]
    public sealed class InvalidBlockBytesLengthException : BlockPolicyViolationException
    {
        /// <summary>
        /// Initializes a new instance of <see cref="InvalidBlockBytesLengthException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="bytesLength">The invalid <see cref="Block{T}.BytesLength"/>
        /// according to <see cref="IBlockPolicy{T}"/>.</param>
        public InvalidBlockBytesLengthException(string message, int bytesLength)
            : base(message)
        {
            BytesLength = bytesLength;
        }

        private InvalidBlockBytesLengthException(SerializationInfo info, StreamingContext context)
            : base(info.GetString(nameof(Message)) ?? string.Empty)
        {
            BytesLength = info.GetInt32(nameof(BytesLength));
        }

        public int BytesLength { get; private set; }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(BytesLength), BytesLength);
        }
    }
}
