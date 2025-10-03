using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Libplanet.Serialization.Analyzers.TypeUtility;

namespace Libplanet.Serialization.Analyzers.Items;

internal sealed class LIBP1002 : ItemBase
{
    public override void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var modelClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is TypeDeclarationSyntax typeDecl && typeDecl.AttributeLists.Count > 0,
                transform: static (ctx, _) => (
                    Declaration: (TypeDeclarationSyntax)ctx.Node,
                    ctx.SemanticModel
                ));

        context.RegisterSourceOutput(modelClasses, (spc, classInfo) =>
        {
            var declaration = classInfo.Declaration;
            var semanticModel = classInfo.SemanticModel;
            if (!TryGetModelScalarKind(declaration, semanticModel, out var kind))
            {
                return;
            }

            var returnType = ModelScalarKindToType(kind);
            if (!ContainsToScalarValueMethod(declaration, semanticModel, returnType))
            {
                var message = "The type '{0}' with [Libplanet.Serialization.ModelScalarAttribute] attribute must " +
                              "have a method 'ToScalarValue' that takes no parameters and returns a scalar value " +
                              "of type '{1}'.";
                var descriptor = new DiagnosticDescriptor(
                    id: "LIBP1002",
                    title: "Model scalar class must have ToScalarValue method",
                    messageFormat: message,
                    category: "Libplanet.Serialization",
                    defaultSeverity: DiagnosticSeverity.Error,
                    isEnabledByDefault: true);

                var diagnostic = Diagnostic.Create(
                    descriptor,
                    declaration.GetLocation(),
                    declaration.Identifier.Text,
                    returnType.FullName);
                spc.ReportDiagnostic(diagnostic);
            }
        });
    }

    private static bool ContainsToScalarValueMethod(
        TypeDeclarationSyntax declaration, SemanticModel semanticModel, Type returnType)
    {
        var methods = declaration.Members.OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            if (method.Identifier.Text != "ToScalarValue")
            {
                continue;
            }

            if (method.ParameterList.Parameters.Count != 0)
            {
                continue;
            }

            if (EqualsType(semanticModel, returnType, method.ReturnType))
            {
                return true;
            }
        }

        return false;
    }
}
