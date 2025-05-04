using Bencodex.Types;
using Libplanet.Types.Tx;

namespace Libplanet.Types.Converters;

internal sealed class ActionBytecodeTypeConverter : TypeConverterBase<ActionBytecode, Binary>
{
    protected override ActionBytecode ConvertFromValue(Binary value) => new(value.ByteArray);

    protected override Binary ConvertToValue(ActionBytecode value) => new(value.Bytes);

    protected override ActionBytecode ConvertFromString(string value) => new(ByteUtility.ParseHexToImmutable(value));

    protected override string ConvertToString(ActionBytecode value) => ByteUtility.Hex(value.Bytes);
}
