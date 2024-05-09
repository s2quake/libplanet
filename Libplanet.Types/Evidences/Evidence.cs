using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Bencodex;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Types.Blocks;

namespace Libplanet.Types.Evidences
{
    /// <summary>
    /// Abstraction of evidence that abstracts common functionalities of various evidences.
    /// </summary>
    public abstract class Evidence
        : IEquatable<Evidence>, IComparable<Evidence>, IComparable, IBencodable
    {
        public const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.ffffffZ";

        private const string TypeKey = "type";
        private const string DataKey = "data";

        private static readonly Dictionary<string, Func<IValue, Evidence>> CreatorByType
            = new Dictionary<string, Func<IValue, Evidence>>();

        private static readonly Codec Codec = new Codec();
        private static readonly byte[] HeightKey = { 0x68 };              // 'h'
        private static readonly byte[] ValidatorKey = { 0x76, 0x61 };     // 'va'
        private static readonly byte[] TimestampKey = { 0x74 };           // 't'
        private EvidenceId? _id;

        static Evidence()
        {
            Register(FalseEvidence.FalseEvidenceType, value =>
                new FalseEvidence(value));

            Register(DuplicateVoteEvidence.DuplicateVoteEvidenceType, value =>
                new DuplicateVoteEvidence(value));
        }

        protected Evidence(long height, Address targetAddress, DateTimeOffset timestamp)
        {
            Height = height;
            TargetAddress = targetAddress;
            Timestamp = timestamp;
        }

        protected Evidence(IValue bencoded)
        {
            if (bencoded is Dictionary dictionary)
            {
                Height = dictionary.GetValue<Integer>(HeightKey);
                TargetAddress = new Address(dictionary.GetValue<IValue>(ValidatorKey));
                Timestamp = DateTimeOffset.ParseExact(
                    dictionary.GetValue<Text>(TimestampKey),
                    TimestampFormat,
                    CultureInfo.InvariantCulture);
            }
            else
            {
                throw new ArgumentException(
                    "Given bencoded must be of type Dictionary.", nameof(bencoded));
            }
        }

        /// <summary>
        /// Type of current evidence.
        /// </summary>
        public abstract string Type { get; }

        /// <summary>
        /// Block height that infraction has been occured.
        /// </summary>
        public long Height { get; }

        /// <summary>
        /// Address of the target that committed the infraction.
        /// </summary>
        public Address TargetAddress { get; }

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
        [JsonIgnore]
        public IValue Bencoded
        {
            get
            {
                Dictionary bencoded = Dictionary.Empty
                    .Add(HeightKey, Height)
                    .Add(ValidatorKey, TargetAddress.Bencoded)
                    .Add(
                        TimestampKey,
                        Timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture));
                bencoded = OnBencoded(bencoded);
                return bencoded;
            }
        }

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
            Dictionary bencoded = Dictionary.Empty
                .Add(TypeKey, evidence.Type)
                .Add(DataKey, evidence.Bencoded);
            return bencoded;
        }

        public static void Register(string evidenceType, Func<IValue, Evidence> creator)
        {
            CreatorByType.Add(evidenceType, creator);
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
            var type = (string)((Dictionary)value).GetValue<Text>(TypeKey);
            var data = ((Dictionary)value).GetValue<IValue>(DataKey);
            if (CreatorByType.TryGetValue(type, out Func<IValue, Evidence>? creator))
            {
                return creator(data);
            }
            else
            {
                throw new ArgumentException($"Unknown evidence type: {type}");
            }
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

        protected abstract Dictionary OnBencoded(Dictionary dictionary);

        protected abstract void Verify(Block block);
    }
}
