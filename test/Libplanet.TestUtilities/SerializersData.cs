namespace Libplanet.TestUtilities;

public class SerializersData : TheoryData<string>
{
    public SerializersData()
    {
        Add("binary");
        Add("json");
        Add("yaml");
    }
}
