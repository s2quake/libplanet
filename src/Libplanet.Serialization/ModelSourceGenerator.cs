using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Libplanet.Serialization.Descriptors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Libplanet.Serialization;

[Generator]
public class ModelSourceGenerator : ISourceGenerator
{
    private const string ModelAttributeFullName = "Libplanet.Serialization.ModelAttribute";
    
    public void Execute(GeneratorExecutionContext context)
    {
        // Check if we have a syntax receiver
        if (!(context.SyntaxContextReceiver is ModelSyntaxReceiver receiver))
        {
            return;
        }

        // Get the ModelAttribute symbol for comparison
        INamedTypeSymbol? modelAttributeSymbol = context.Compilation.GetTypeByMetadataName(ModelAttributeFullName);
        if (modelAttributeSymbol == null)
        {
            // ModelAttribute type not found in compilation
            return;
        }

        // Process each candidate class or struct
        foreach (var candidate in receiver.Candidates)
        {
            // Get the semantic model for this syntax node
            var semanticModel = context.Compilation.GetSemanticModel(candidate.SyntaxTree);
            var typeSymbol = semanticModel.GetDeclaredSymbol(candidate) as INamedTypeSymbol;
            
            if (typeSymbol == null)
            {
                continue;
            }
            
            // Check if the type has ModelAttribute
            var hasModelAttribute = typeSymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.Equals(modelAttributeSymbol, SymbolEqualityComparer.Default) == true);
                
            if (hasModelAttribute)
            {
                // Generate source code for this model
                ProcessModel(context, typeSymbol);
            }
        }
    }
    
    private void ProcessModel(GeneratorExecutionContext context, INamedTypeSymbol typeSymbol)
    {
        // For now, just generate a simple source file that indicates the model was found
        // This will be expanded later to include actual implementation
        string nameSpace = typeSymbol.ContainingNamespace.ToDisplayString();
        string className = typeSymbol.Name;
        
        StringBuilder sourceBuilder = new StringBuilder();
        sourceBuilder.AppendLine($"// Auto-generated code for {className}");
        sourceBuilder.AppendLine("using System;");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine($"namespace {nameSpace}");
        sourceBuilder.AppendLine("{");
        sourceBuilder.AppendLine($"    // Generated helper class for {className}");
        sourceBuilder.AppendLine($"    public static class {className}ModelHelper");
        sourceBuilder.AppendLine("    {");
        sourceBuilder.AppendLine($"        public static string ModelName => \"{className}\";");
        sourceBuilder.AppendLine("    }");
        sourceBuilder.AppendLine("}");
        
        // Add the source code to the compilation
        string fileName = $"{className}ModelHelper.g.cs";
        context.AddSource(fileName, SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
    }

    public void Initialize(GeneratorInitializationContext context)
    {
        // Register a syntax receiver to identify potential targets for code generation
        context.RegisterForSyntaxNotifications(() => new ModelSyntaxReceiver());
    }
    
    /// <summary>
    /// Syntax receiver to collect candidate TypeDeclarationSyntax nodes for processing.
    /// </summary>
    private class ModelSyntaxReceiver : ISyntaxContextReceiver
    {
        public List<TypeDeclarationSyntax> Candidates { get; } = new List<TypeDeclarationSyntax>();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            // Look for classes and structs
            if (context.Node is ClassDeclarationSyntax classDecl)
            {
                // Check if it might have our attribute by examining attribute lists
                if (classDecl.AttributeLists.Count > 0)
                {
                    Candidates.Add(classDecl);
                }
            }
            else if (context.Node is StructDeclarationSyntax structDecl)
            {
                // Check if it might have our attribute by examining attribute lists
                if (structDecl.AttributeLists.Count > 0)
                {
                    Candidates.Add(structDecl);
                }
            }
        }
    }
}
