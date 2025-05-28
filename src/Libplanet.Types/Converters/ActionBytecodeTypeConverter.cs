namespace Libplanet.Types.Converters;

internal sealed class ActionBytecodeTypeConverter : TypeConverterBase<ActionBytecode>
{
    protected override ActionBytecode ConvertFromString(string value) => new(ByteUtility.ParseHexToImmutable(value));

    protected override string ConvertToString(ActionBytecode value) => ByteUtility.Hex(value.Bytes);
}
