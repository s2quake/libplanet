using System.Runtime.InteropServices;
using System.Text;
using Libplanet.State;
using Libplanet.Types;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Libplanet.Node.Tests.Services;

internal static class RuntimeCompiler
{
    public static void CompileCode(string code, string assemblyName, string assemblyPath)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ImmutableDictionary<,>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IAction).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(PublicKey).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Block).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IActionProvider).Assembly.Location),
            MetadataReference.CreateFromFile(GetRuntimeLibraryPath("netstandard.dll")),
            MetadataReference.CreateFromFile(GetRuntimeLibraryPath("System.Runtime.dll")),
            MetadataReference.CreateFromFile(
                GetRuntimeLibraryPath("System.Collections.Immutable.dll")),
        };

        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
        var compilation = CSharpCompilation.Create(
            assemblyName, [syntaxTree], references, options);

        // 어셈블리 스트림 생성
        using var fs = new FileStream(assemblyPath, FileMode.Create);
        var result = compilation.Emit(fs);

        if (!result.Success)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Compilation failed.");
            foreach (var diagnostic in result.Diagnostics)
            {
                sb.AppendLine(diagnostic.ToString());
            }

            throw new InvalidOperationException(sb.ToString());
        }

        fs.Close();
    }

    private static string GetRuntimeLibraryPath(string name)
        => Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), name);
}
