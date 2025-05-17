using System.Text;

namespace Libplanet.Serialization.ModelConverters;

internal sealed class StringTypeConverter : InternalModelConverterBase<string>
{
    protected override string ConvertFromValue(byte[] value) => Encoding.UTF8.GetString(value);

    protected override byte[] ConvertToValue(string value) => Encoding.UTF8.GetBytes(value);
}
