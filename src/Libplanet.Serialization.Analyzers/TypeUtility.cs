using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Libplanet.Serialization.Analyzers;

internal static class TypeUtility
{
    public static bool IsScalarModel(TypeDeclarationSyntax typeDeclaration, SemanticModel semanticModel)
    {
        var query = from attributeList in typeDeclaration.AttributeLists
                    from attribute in attributeList.Attributes
                    let symbol = semanticModel.GetSymbolInfo(attribute).Symbol
                    where symbol != null
                    let attributeType = symbol.ContainingType
                    where attributeType.ToDisplayString() == "Libplanet.Serialization.ModelScalarAttribute"
                    select attributeType;

        return query.Any();
    }

    public static bool TryGetModelScalarKind(
        TypeDeclarationSyntax typeDeclaration, SemanticModel semanticModel, out string kind)
    {
        kind = string.Empty;
        var query = from attributeList in typeDeclaration.AttributeLists
                    from attribute in attributeList.Attributes
                    let symbol = semanticModel.GetSymbolInfo(attribute).Symbol
                    where symbol != null
                    let attributeType = symbol.ContainingType
                    where attributeType.ToDisplayString() == "Libplanet.Serialization.ModelScalarAttribute"
                    select attribute;

        var modelScalarAttribute = query.FirstOrDefault();
        if (modelScalarAttribute == null)
        {
            return false;
        }

        var args = modelScalarAttribute.ArgumentList?.Arguments ?? Enumerable.Empty<AttributeArgumentSyntax>();

        foreach (var arg in args)
        {
            if (arg.NameEquals?.Name.Identifier.Text == "Kind")
            {
                var expr = arg.Expression;
                if (expr is MemberAccessExpressionSyntax memberAccess)
                {
                    kind = memberAccess.Name.Identifier.Text;
                    return true;
                }
                else if (expr is IdentifierNameSyntax identifierName)
                {
                    kind = identifierName.Identifier.Text;
                    return true;
                }
            }
        }

        return false;
    }

    public static Type ModelScalarKindToType(string kind) => kind switch
    {
        "String" => typeof(string),
        "Boolean" => typeof(bool),
        "Int32" => typeof(int),
        "Int64" => typeof(long),
        "Hex" => typeof(byte[]),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static ITypeSymbol GetTypeSymbol(SemanticModel semanticModel, Type returnType)
    {
        if (returnType.IsArray)
        {
            var elementSymbol = GetTypeSymbol(semanticModel, returnType.GetElementType()!);
            return semanticModel.Compilation.CreateArrayTypeSymbol(elementSymbol);
        }

        return semanticModel.Compilation.GetTypeByMetadataName(returnType.FullName!)
            ?? throw new ArgumentException($"Type '{returnType}' is not found in the compilation.", nameof(returnType));
    }

    public static bool EqualsType(SemanticModel semanticModel, Type type, ExpressionSyntax expressionSyntax)
    {
        var typeSymbol = GetTypeSymbol(semanticModel, type);
        var typeInfo = semanticModel.GetTypeInfo(expressionSyntax);
        return SymbolEqualityComparer.Default.Equals(typeSymbol, typeInfo.Type);
    }

    public static bool EqualsType(
        SemanticModel semanticModel, TypeDeclarationSyntax declaration, ExpressionSyntax expressionSyntax)
    {
        var type1 = semanticModel.GetDeclaredSymbol(declaration);
        var type2 = semanticModel.GetTypeInfo(expressionSyntax).Type;
        if (type1 is null || type2 is null)
        {
            return false;
        }

        return SymbolEqualityComparer.Default.Equals(type1.OriginalDefinition, type2.OriginalDefinition);
    }
}