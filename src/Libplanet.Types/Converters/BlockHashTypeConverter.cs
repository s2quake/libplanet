using Bencodex.Types;
using Libplanet.Types.Blocks;

namespace Libplanet.Types.Converters;

internal sealed class BlockHashTypeConverter : TypeConverterBase<BlockHash, Binary>
{
    protected override BlockHash ConvertFromValue(Binary value) => new(value.ToByteArray());

    protected override Binary ConvertToValue(BlockHash value) => new(value.Bytes);

    protected override BlockHash ConvertFromString(string value) => BlockHash.Parse(value);

    protected override string ConvertToString(BlockHash value) => $"{value:raw}";
}
