using System.Threading.Tasks;
using GraphQL;
using GraphQL.Execution;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Explorer.GraphTypes;
using Xunit;
using static Libplanet.Explorer.Tests.GraphQLTestUtils;

namespace Libplanet.Explorer.Tests.GraphTypes;

public class CurrencyTypeTest
{
    [Fact]
    public async Task Query()
    {
        var addrA = Address.Parse("D6D639DA5a58A78A564C2cD3DB55FA7CeBE244A9");
        var addrB = Address.Parse("5003712B63baAB98094aD678EA2B24BcE445D076");
        var currency = new Currency(
            "ABC", 2, 1234567, [addrA, addrB]);
        ExecutionResult result = await ExecuteQueryAsync<CurrencyType>(@"
        {
            ticker
            decimalPlaces
            minters
            maximumSupply {
                currency {
                    ticker
                    hash
                }
                sign
                majorUnit
                minorUnit
                quantity
                string
            }
            hash
        }", source: currency);
        Assert.Null(result.Errors);
        ExecutionNode resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        IDictionary<string, object> resultDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        Assert.Equal("ABC", resultDict["ticker"]);
        Assert.Equal((byte)2, resultDict["decimalPlaces"]);
        IDictionary<string, object> maxSupplyDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(resultDict["maximumSupply"]);
        IDictionary<string, object> currencyDict =
            Assert.IsAssignableFrom<IDictionary<string, object>>(maxSupplyDict["currency"]);
        Assert.Equal("ABC", currencyDict["ticker"]);
        Assert.Equal("4bc1a79e2f30892acbff9fc7e5c71e2aea112110", currencyDict["hash"]);
        Assert.Equal(1, Assert.IsAssignableFrom<int>(maxSupplyDict["sign"]));
        Assert.Equal(12345, Assert.IsAssignableFrom<BigInteger>(maxSupplyDict["majorUnit"]));
        Assert.Equal(67, Assert.IsAssignableFrom<BigInteger>(maxSupplyDict["minorUnit"]));
        Assert.Equal("12345.67", maxSupplyDict["quantity"]);
        Assert.Equal("12345.67 ABC", maxSupplyDict["string"]);
        object[] minters = Assert.IsAssignableFrom<object[]>(resultDict["minters"]);
        Assert.All(minters, m => Assert.IsType<string>(m));
        Assert.Equal(minters.Cast<string>().ToArray(), new[]
        {
            "0x5003712B63baAB98094aD678EA2B24BcE445D076",
            "0xD6D639DA5a58A78A564C2cD3DB55FA7CeBE244A9",
        });
        Assert.Equal("4bc1a79e2f30892acbff9fc7e5c71e2aea112110", resultDict["hash"]);

        currency = new Currency("NOMINTER", 2);
        result = await ExecuteQueryAsync<CurrencyType>(
            @"{
                minters
                maximumSupply { quantity }
            }",
            source: currency
        );
        Assert.Null(result.Errors);
        resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        resultDict = Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        Assert.Null(resultDict["minters"]);
        Assert.Null(resultDict["maximumSupply"]);

#pragma warning disable CS0618
        currency = new Currency("LEGACY", 2);
#pragma warning restore CS0618
        result = await ExecuteQueryAsync<CurrencyType>(
            @"{
                maximumSupply { quantity }
            }",
            source: currency
        );
        Assert.Null(result.Errors);
        resultData = Assert.IsAssignableFrom<ExecutionNode>(result.Data);
        resultDict = Assert.IsAssignableFrom<IDictionary<string, object>>(resultData!.ToValue());
        Assert.Null(resultDict["maximumSupply"]);
    }
}
