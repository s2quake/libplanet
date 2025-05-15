using Libplanet.Types.Tx;

namespace Libplanet.Types.Converters;

internal sealed class ActionBytecodeTypeConverter : TypeConverterBase<ActionBytecode>
{
    protected override ActionBytecode ConvertFromValue(byte[] value) => new([.. value]);

    protected override byte[] ConvertToValue(ActionBytecode value) => [.. value.Bytes];

    protected override ActionBytecode ConvertFromString(string value) => new(ByteUtility.ParseHexToImmutable(value));

    protected override string ConvertToString(ActionBytecode value) => ByteUtility.Hex(value.Bytes);
}
