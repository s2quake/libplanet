using GraphQL.Language.AST;
using GraphQL.Types;
using Libplanet.Types;
using Libplanet.Types;

namespace Libplanet.Explorer.GraphTypes
{
    public class TxIdType : StringGraphType
    {
        public TxIdType()
        {
            Name = "TxId";
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
                return new TxId(ByteUtility.ParseHex(str));
            }

            return ThrowValueConversionError(value);
        }

        public override object? Serialize(object? value)
        {
            if (value is null)
            {
                return null;
            }

            if (value is TxId txId)
            {
                return ByteUtility.Hex(txId.Bytes);
            }

            return ThrowSerializationError(value);
        }
    }
}
