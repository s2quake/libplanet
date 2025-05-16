using System.Text;

namespace Libplanet.Serialization.Converters;

internal sealed class StringTypeConverter : InternalTypeConverterBase<string>
{
    protected override string ConvertFromValue(byte[] value) => Encoding.UTF8.GetString(value);

    protected override byte[] ConvertToValue(string value) => Encoding.UTF8.GetBytes(value);
}
