using System;
using System.Globalization;
using System.Text.Json.Serialization;
using Bencodex;
using Bencodex.Types;
using Libplanet.Crypto;

namespace Libplanet.Types.Consensus
{
    public sealed class FalseEvidence
        : Evidence, IEquatable<FalseEvidence>, IBencodable
    {
        private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.ffffffZ";
        private static readonly byte[] HeightKey = { 0x68 };              // 'h'
        private static readonly byte[] ValidatorKey = { 0x76 };           // 'v'
        private static readonly byte[] TimestampKey = { 0x74 };           // 't'

        public FalseEvidence(IValue bencoded)
            : this(bencoded is Dictionary dict
                ? dict
                : throw new ArgumentException(
                    $"Given {nameof(bencoded)} must be of type " +
                    $"{typeof(Dictionary)}: {bencoded.GetType()}",
                    nameof(bencoded)))
        {
        }

        public FalseEvidence(
            long height,
            Address validatorAddress,
            DateTimeOffset timestamp)
            : base(height, timestamp)
        {
            if (height < 0L)
            {
                throw new ArgumentException(
                    $"Height is not positive");
            }

            ValidatorAddress = validatorAddress;
        }

        private FalseEvidence(Dictionary bencoded)
            : this(
                height: bencoded.GetValue<Integer>(HeightKey),
                validatorAddress: new Address(bencoded.GetValue<IValue>(ValidatorKey)),
                timestamp: DateTimeOffset.ParseExact(
                    bencoded.GetValue<Text>(TimestampKey),
                    TimestampFormat,
                    CultureInfo.InvariantCulture))
        {
        }

        public override EvidenceType Type => EvidenceType.FalseEvidence;

        public Address ValidatorAddress { get; }

        /// <inheritdoc/>
        [JsonIgnore]
        public override IValue Bencoded
        {
            get
            {
                Dictionary bencoded = Dictionary.Empty
                    .Add(HeightKey, Height)
                    .Add(ValidatorKey, ValidatorAddress.Bencoded)
                    .Add(
                        TimestampKey,
                        Timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture));
                return bencoded;
            }
        }

        /// <inheritdoc/>
        public bool Equals(FalseEvidence? other)
            => other is FalseEvidence emptyEvidence &&
                Height == emptyEvidence.Height &&
                ValidatorAddress == emptyEvidence.ValidatorAddress &&
                Timestamp
                    .ToString(TimestampFormat, CultureInfo.InvariantCulture).Equals(
                        emptyEvidence.Timestamp.ToString(
                            TimestampFormat,
                            CultureInfo.InvariantCulture));

        /// <inheritdoc/>
        public override bool Equals(object? obj)
            => obj is FalseEvidence other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
            => HashCode.Combine(
                Height,
                ValidatorAddress.ToString(),
                Timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture));
    }
}
