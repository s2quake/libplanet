using Libplanet.Types.Blocks;

namespace Libplanet.Types.Converters;

internal sealed class BlockHashTypeConverter : TypeConverterBase<BlockHash>
{
    protected override BlockHash ConvertFromString(string value) => BlockHash.Parse(value);

    protected override string ConvertToString(BlockHash value) => $"{value:raw}";
}
