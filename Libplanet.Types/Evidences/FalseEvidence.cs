using System;
using System.Globalization;
using Bencodex;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Types.Blocks;

namespace Libplanet.Types.Evidences
{
    public sealed class FalseEvidence
        : Evidence, IEquatable<FalseEvidence>, IBencodable
    {
        public const string FalseEvidenceType = "FalseEvidence";

        static FalseEvidence()
        {
            Register(FalseEvidenceType, value => new FalseEvidence(value));
        }

        public FalseEvidence(IValue bencoded)
            : base(bencoded)
        {
        }

        public FalseEvidence(
            long height,
            Address validatorAddress,
            DateTimeOffset timestamp)
            : base(height, validatorAddress, timestamp)
        {
            if (height < 0L)
            {
                throw new ArgumentException(
                    $"Height is not positive");
            }
        }

        public override string Type => FalseEvidenceType;

        public Address ValidatorAddress => TargetAddress;

        /// <inheritdoc/>
        public bool Equals(FalseEvidence? other) => base.Equals(other);

        /// <inheritdoc/>
        public override bool Equals(object? obj)
            => obj is FalseEvidence other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
            => HashCode.Combine(
                Height,
                ValidatorAddress.ToString(),
                Timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture));

        protected override Dictionary OnBencoded(Dictionary dictionary)
            => dictionary;

        protected override void Verify(Block block)
        {
            throw new NotImplementedException();
        }
    }
}
