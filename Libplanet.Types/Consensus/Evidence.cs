using System;
using System.Security.Cryptography;
using System.Transactions;
using Bencodex;
using Bencodex.Types;

namespace Libplanet.Types.Consensus
{
    /// <summary>
    /// Abstraction of evidence that abstracts common functionalities of various evidences.
    /// </summary>
    public abstract class Evidence
        : IEquatable<Evidence>, IComparable<Evidence>, IComparable, IBencodable
    {
        private const string TypeKey = "type";
        private const string EvidenceKey = "evidence";
        private static readonly Codec Codec = new Codec();
        private EvidenceId? _id;

        protected Evidence(long height, DateTimeOffset timestamp)
        {
            Height = height;
            Timestamp = timestamp;
        }

        /// <summary>
        /// Types of evidences.
        /// </summary>
        public enum EvidenceType : byte
        {
            /// <summary>
            /// Evidence for test.
            /// </summary>
            FalseEvidence = 0x01,

            /// <summary>
            /// Evidence for duplicated votes.
            /// </summary>
            DuplicateVoteEvidence = 0x02,
        }

        /// <summary>
        /// Type of current evidence.
        /// </summary>
        public abstract EvidenceType Type { get; }

        /// <summary>
        /// Block height that infraction has been occured.
        /// </summary>
        public long Height { get; }

        /// <summary>
        /// Timestamp that indicates the time that evidence has been made.
        /// </summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>
        /// Identifier for evidence.
        /// </summary>
        public EvidenceId Id
        {
            get
            {
                if (!(_id is { } nonNull))
                {
                    using var hasher = SHA256.Create();
                    byte[] payload = Codec.Encode(Bencoded);
                    _id = nonNull = new EvidenceId(hasher.ComputeHash(payload));
                }

                return nonNull;
            }
        }

        /// <summary>
        /// Inner bencoded form of evidence.
        /// This method won't bencode evidence type, so we can't decode evidence
        /// without knowing the evidence type.
        /// For fully bencoded form, use <see cref="Bencode(Evidence)"/>.
        /// </summary>
        public abstract IValue Bencoded { get; }

        /// <summary>
        /// Bencode <see cref="Evidence"/>.
        /// This bencodes <paramref name="evidence"/> with its evidence type,
        /// so its return can be decoded by <see cref="Decode(IValue)"/>
        /// without concrete evidence type.
        /// </summary>
        /// <param name="evidence"><see cref="Evidence"/> to be bencoded.</param>
        /// <returns>Bencoded <see cref="IValue"/>.</returns>
        public static IValue Bencode(Evidence evidence)
        {
            Dictionary bencoded = Bencodex.Types.Dictionary.Empty
                .Add(TypeKey, (int)evidence.Type)
                .Add(EvidenceKey, evidence.Bencoded);
            return bencoded;
        }

        /// <summary>
        /// Decode <see cref="IValue"/> that bencoded with <see cref="Bencode(Evidence)"/>
        /// to <see cref="Evidence"/>.
        /// </summary>
        /// <param name="value">Bencoded <see cref="IValue"/> to be decoded.</param>
        /// <returns>Decoded <see cref="Evidence"/>.</returns>
        /// <exception cref="InvalidCastException">Thrown when <paramref name="value"/>
        /// cannot be decoded as an evidence.</exception>
        public static Evidence Decode(IValue value)
        {
            var type = (EvidenceType)(int)((Dictionary)value).GetValue<Integer>(TypeKey);
            var evidence = ((Dictionary)value).GetValue<IValue>(EvidenceKey);
            return type switch
            {
                EvidenceType.FalseEvidence => new FalseEvidence(evidence),
                EvidenceType.DuplicateVoteEvidence => new DuplicatedVoteEvidence(evidence),
                _ => throw new InvalidCastException($"Given type {type} is not a valid evidence."),
            };
        }

        public static Evidence Deserialize(byte[] bytes)
        {
            return Decode(Codec.Decode(bytes));
        }

        public byte[] Serialize()
        {
            return Codec.Encode(Bencode(this));
        }

        /// <inheritdoc/>
        public bool Equals(Evidence? other) => Id.Equals(other?.Id);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is Evidence other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => unchecked((17 * 31 + Id.GetHashCode()) * 31);

        /// <inheritdoc cref="IComparable{T}.CompareTo(T)"/>
        public int CompareTo(Evidence? other)
        {
            return Id.CompareTo(other?.Id);
        }

        /// <inheritdoc cref="IComparable.CompareTo(object)"/>
        public int CompareTo(object? obj) => obj is Evidence other
            ? CompareTo(other: other)
            : throw new ArgumentException(
                $"Argument {nameof(obj)} is not a ${nameof(Evidence)}.", nameof(obj));
    }
}
