using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Libplanet.Serialization.Analyzers;

internal static class MethodDeclarationSyntaxExtensions
{
    public static bool IsStatic(this MethodDeclarationSyntax @this)
        => @this.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
}
