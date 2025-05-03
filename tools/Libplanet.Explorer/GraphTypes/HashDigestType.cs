using System.Security.Cryptography;
using GraphQL.Language.AST;
using GraphQL.Types;
using Libplanet.Types;

namespace Libplanet.Explorer.GraphTypes
{
    public class HashDigestType<T> : StringGraphType
        where T : HashAlgorithm
    {
        public HashDigestType()
        {
            Name = $"HashDigest{typeof(T).Name}";
        }

        public override object? ParseLiteral(IValue value)
        {
            if (value is StringValue stringValue)
            {
                return ParseValue(stringValue.Value);
            }

            if (value is NullValue)
            {
                return null;
            }

            return ThrowLiteralConversionError(value);
        }

        public override object? ParseValue(object? value)
        {
            if (value is null)
            {
                return null;
            }

            if (value is string str)
            {
                return new HashDigest<T>(ByteUtility.ParseHex(str));
            }

            return ThrowValueConversionError(value);
        }

        public override object? Serialize(object? value)
        {
            if (value is null)
            {
                return null;
            }

            if (value is HashDigest<T> hashDigest)
            {
                return ByteUtility.Hex(hashDigest.Bytes);
            }

            return ThrowSerializationError(value);
        }
    }
}
