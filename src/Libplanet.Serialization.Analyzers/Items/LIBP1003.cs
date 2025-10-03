using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Libplanet.Serialization.Analyzers.TypeUtility;

namespace Libplanet.Serialization.Analyzers.Items;

internal sealed class LIBP1003 : ItemBase
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

            var parameterType = ModelScalarKindToType(kind);
            if (!ContainsFromScalarValueMethod(declaration, semanticModel, parameterType))
            {
                var message = "The type '{0}' with [Libplanet.Serialization.ModelScalarAttribute] attribute must " +
                              "have a static method 'FromScalarValue' that takes two parameters: " +
                              $"an '{typeof(IServiceProvider).FullName}' and a '{parameterType.FullName}', " +
                              $"and returns an instance of '{declaration.Identifier.Text}'.";
                var descriptor = new DiagnosticDescriptor(
                    id: "LIBP1003",
                    title: "Model scalar class must have FromScalarValue method",
                    messageFormat: message,
                    category: "Libplanet.Serialization",
                    defaultSeverity: DiagnosticSeverity.Error,
                    isEnabledByDefault: true);

                var diagnostic = Diagnostic.Create(
                    descriptor,
                    declaration.GetLocation(),
                    declaration.Identifier.Text,
                    parameterType.FullName);

                spc.ReportDiagnostic(diagnostic);
            }
        });
    }

    private static bool ContainsFromScalarValueMethod(
        TypeDeclarationSyntax declaration, SemanticModel semanticModel, Type parameterType)
    {
        return declaration.Members.OfType<MethodDeclarationSyntax>().SingleOrDefault(Predicate) != null;

        bool Predicate(MethodDeclarationSyntax method)
        {
            if (method.Identifier.Text != "FromScalarValue")
            {
                return false;
            }

            if (!method.IsStatic())
            {
                return false;
            }

            if (!EqualsType(semanticModel, declaration, method.ReturnType))
            {
                return false;
            }

            if (method.ParameterList.Parameters.Count is not 2)
            {
                return false;
            }

            if (method.ParameterList.Parameters[0].Type is not { } serviceProviderTypeSyntax)
            {
                return false;
            }

            if (method.ParameterList.Parameters[1].Type is not { } parameterTypeSyntax)
            {
                return false;
            }

            return EqualsType(semanticModel, typeof(IServiceProvider), serviceProviderTypeSyntax) &&
                   EqualsType(semanticModel, parameterType, parameterTypeSyntax);
        }
    }
}
