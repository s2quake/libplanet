using System;
using System.Globalization;
using System.Numerics;
using System.Text.Json.Serialization;
using Bencodex;
using Bencodex.Misc;
using Bencodex.Types;

namespace Libplanet.Types.Consensus
{
    public sealed class EmptyEvidence
        : Evidence, IEquatable<EmptyEvidence>, IBencodable
    {
        private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.ffffffZ";
        private static readonly byte[] HeightKey = { 0x68 };              // 'h'
        private static readonly byte[] VoteRefKey = { 0x76 };             // 'v'
        private static readonly byte[] VoteDupKey = { 0x56 };             // 'V'
        private static readonly byte[] ValidatorPowerKey = { 0x70 };      // 'p'
        private static readonly byte[] TotalPowerKey = { 0x50 };          // 'P'
        private static readonly byte[] TimestampKey = { 0x74 };           // 't'

        public EmptyEvidence(Bencodex.Types.IValue bencoded)
            : this(bencoded is Bencodex.Types.Dictionary dict
                ? dict
                : throw new ArgumentException(
                    $"Given {nameof(bencoded)} must be of type " +
                    $"{typeof(Bencodex.Types.Dictionary)}: {bencoded.GetType()}",
                    nameof(bencoded)))
        {
        }

        public EmptyEvidence(
            long height,
            DateTimeOffset timestamp)
            : base(height, timestamp)
        {
            if (height < 0L)
            {
                throw new ArgumentException(
                    $"Height is not positive");
            }
        }

        private EmptyEvidence(Bencodex.Types.Dictionary bencoded)
            : this(
                height: bencoded.GetValue<Integer>(HeightKey),
                timestamp: DateTimeOffset.ParseExact(
                    bencoded.GetValue<Text>(TimestampKey),
                    TimestampFormat,
                    CultureInfo.InvariantCulture))
        {
        }

        public override EvidenceType Type => EvidenceType.Empty;

        /// <inheritdoc/>
        [JsonIgnore]
        public override Bencodex.Types.IValue Bencoded
        {
            get
            {
                Dictionary bencoded = Bencodex.Types.Dictionary.Empty
                    .Add(HeightKey, Height)
                    .Add(
                        TimestampKey,
                        Timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture));
                return bencoded;
            }
        }

        public static (Vote, Vote) OrderDuplicateVotePair(Vote voteRef, Vote voteDup)
        {
            if (voteRef.BlockHash is { } voteRefHash)
            {
            }
            else
            {
                throw new ArgumentException(
                    $"voteRef is nill vote");
            }

            if (voteDup.BlockHash is { } voteDupHash)
            {
            }
            else
            {
                throw new ArgumentException(
                    $"voteDup is nill vote");
            }

            if (voteRef.Timestamp < voteDup.Timestamp)
            {
                return (voteRef, voteDup);
            }
            else if (voteRef.Timestamp > voteDup.Timestamp)
            {
                return (voteDup, voteRef);
            }
            else
            {
                if (voteRefHash.CompareTo(voteDupHash) < 0)
                {
                    return (voteRef, voteDup);
                }
                else
                {
                    return (voteDup, voteRef);
                }
            }
        }

        /// <inheritdoc/>
        public bool Equals(EmptyEvidence? other)
            => other is EmptyEvidence emptyEvidence &&
                Height == emptyEvidence.Height &&
                Timestamp
                    .ToString(TimestampFormat, CultureInfo.InvariantCulture).Equals(
                        emptyEvidence.Timestamp.ToString(
                            TimestampFormat,
                            CultureInfo.InvariantCulture));

        /// <inheritdoc/>
        public override bool Equals(object? obj)
            => obj is EmptyEvidence other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
            => HashCode.Combine(
                Height,
                Timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture));
    }
}
