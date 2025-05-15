using Libplanet.Types.Blocks;

namespace Libplanet.Types.Converters;

internal sealed class BlockHashTypeConverter : TypeConverterBase<BlockHash>
{
    protected override BlockHash ConvertFromValue(byte[] value) => new(value);

    protected override byte[] ConvertToValue(BlockHash value) => [.. value.Bytes];

    protected override BlockHash ConvertFromString(string value) => BlockHash.Parse(value);

    protected override string ConvertToString(BlockHash value) => $"{value:raw}";
}
