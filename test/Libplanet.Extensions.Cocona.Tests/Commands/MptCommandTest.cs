using System.IO;
using Libplanet.Extensions.Cocona.Commands;
using Libplanet.Extensions.Cocona.Configuration;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Tools.Tests.Services;
using Libplanet.Types;

namespace Libplanet.Extensions.Cocona.Tests.Commands;

public class MptCommandTest : IDisposable
{
    private readonly MptCommand _command;
    private readonly string _pathA;
    private readonly string _pathB;
    private readonly ITrie _trieA;
    private readonly ITrie _trieB;

    public MptCommandTest()
    {
        _command = new MptCommand();

        _pathA = NewTempPath();
        _pathB = NewTempPath();
        using var stateKeyValueStoreA = new DefaultTable(_pathA);
        using var stateKeyValueStoreB = new DefaultTable(_pathB);
        var stateStoreA = new TrieStateStore(new DefaultTable(_pathA));
        var stateStoreB = new TrieStateStore(new DefaultTable(_pathB));
        _trieA = stateStoreA.Commit(
            stateStoreA.GetStateRoot(default)
                .Set("deleted", null)
                .Set("common", "before"));
        _trieB = stateStoreB.Commit(
            stateStoreB.GetStateRoot(default)
                .Set("created", null)
                .Set("common", "after"));
    }

    [Fact]
    public void Diff_PrintsAsJson()
    {
        using StringWriter stringWriter = new StringWriter { NewLine = "\n" };
        var originalOutWriter = Console.Out;
        try
        {
            Console.SetOut(stringWriter);
            var kvStoreUri = $"default://{_pathA}";
            var otherKvStoreUri = $"default://{_pathB}";
            var configuration =
                new ToolConfiguration(new MptConfiguration(new Dictionary<string, string>()));
            string stateRootHashHex = ByteUtility.Hex(_trieA.Hash.Bytes);
            string otherStateRootHashHex = ByteUtility.Hex(_trieB.Hash.Bytes);

            _command.Diff(
                kvStoreUri,
                stateRootHashHex,
                otherKvStoreUri,
                otherStateRootHashHex,
                new TestToolConfigurationService(configuration));

            string expected = string.Format(
                @"{{""Key"":""636f6d6d6f6e"",""StateRootHashToValue"":" +
                @"{{""{0}"":""75353a6166746572"",""{1}"":""75363a6265666f7265""}}}}" + "\n" +
                @"{{""Key"":""64656c65746564""," +
                @"""StateRootHashToValue"":{{""{0}"":""null"",""{1}"":""6e""}}}}" + "\n",
                otherStateRootHashHex,
                stateRootHashHex);
            Assert.Equal(
                expected,
                stringWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOutWriter);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_pathA))
        {
            Directory.Delete(_pathA, true);
        }

        if (Directory.Exists(_pathB))
        {
            Directory.Delete(_pathB, true);
        }
    }

    private static string NewTempPath() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
            .Replace("\\", "/")
            .Replace("C:", string.Empty);
}
