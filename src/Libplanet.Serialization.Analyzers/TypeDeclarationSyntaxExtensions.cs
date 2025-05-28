using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Libplanet.Serialization.Analyzers;

internal static class TypeDeclarationSyntaxExtensions
{
    public static bool IsPublic(this TypeDeclarationSyntax @this)
    {
        return @this.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
    }

    public static bool IsInternal(this TypeDeclarationSyntax @this)
    {
        return @this.Modifiers.Any(m => m.IsKind(SyntaxKind.InternalKeyword));
    }

    public static bool IsProtected(this TypeDeclarationSyntax @this)
    {
        return @this.Modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword));
    }

    public static bool IsPrivate(this TypeDeclarationSyntax @this)
    {
        return @this.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword));
    }

    public static bool IsRecord(this TypeDeclarationSyntax @this)
    {
        return @this is RecordDeclarationSyntax;
    }

    public static bool IsStruct(this TypeDeclarationSyntax @this)
    {
        if (@this is RecordDeclarationSyntax recordDeclaration)
        {
            return recordDeclaration.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword);
        }

        return @this is StructDeclarationSyntax;
    }

    public static bool IsClass(this TypeDeclarationSyntax @this)
    {
        if (@this is RecordDeclarationSyntax recordDeclaration)
        {
            return recordDeclaration.ClassOrStructKeyword.IsKind(SyntaxKind.ClassKeyword);
        }

        return @this is ClassDeclarationSyntax;
    }

    public static string GetNamespace(this TypeDeclarationSyntax @this)
    {
        SyntaxNode? parent = @this.Parent;
        while (parent != null &&
               parent is not NamespaceDeclarationSyntax &&
               parent is not FileScopedNamespaceDeclarationSyntax)
        {
            parent = parent.Parent;
        }

        if (parent is NamespaceDeclarationSyntax namespaceDeclaration)
        {
            return namespaceDeclaration.Name.ToString();
        }

        if (parent is FileScopedNamespaceDeclarationSyntax fileScopedNamespace)
        {
            return fileScopedNamespace.Name.ToString();
        }

        if (parent != null)
        {
            var ns = parent.ToString();
            if (ns.StartsWith("namespace "))
            {
                ns = ns.Substring("namespace ".Length).Trim();
                var braceIndex = ns.IndexOf('{');
                if (braceIndex > 0)
                {
                    ns = ns.Substring(0, braceIndex).Trim();
                }

                return ns;
            }
        }

        return string.Empty;
    }

    public static string GetName(this TypeDeclarationSyntax @this) => @this.Identifier.ValueText;
}
