using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PrimaryConstructor.Readonly;

[AttributeUsage(AttributeTargets.Parameter)]
public class ReadonlyAttribute : Attribute;
    
[Generator]
public class ReadonlyFieldGenerator : IIncrementalGenerator
{
    private const string AttributeName = "ReadonlyAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Find all classes/structs with a primary constructor and at least one parameter with attributes
        var candidateClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => IsCandidate(node),
                transform: (ctx, _) => GetTargetForGeneration(ctx))
            .Where(target => target is not null);

        // 2. Register the source output
        context.RegisterSourceOutput(candidateClasses, Execute);
    }

    private static bool IsCandidate(SyntaxNode node)
    {
        // Must be a Class or Struct declaration
        if (node is not TypeDeclarationSyntax typeDecl)
            return false;

        // Must have a primary constructor parameter list
        if (typeDecl.ParameterList is null)
            return false;

        // Must have at least one parameter with an attribute
        return typeDecl.ParameterList.Parameters.Any(p => p.AttributeLists.Count > 0);
    }

    private static ClassToGenerate? GetTargetForGeneration(GeneratorSyntaxContext context)
    {
        var typeDeclaration = (TypeDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration);

        if (symbol is null) return null;

        var parametersToGenerate = new List<ParameterInfo>();

        // Loop through primary constructor parameters
        foreach (var parameterSyntax in typeDeclaration.ParameterList!.Parameters)
        {
            var paramSymbol = context.SemanticModel.GetDeclaredSymbol(parameterSyntax);
                
            if (paramSymbol is null) continue;

            // Check for [Readonly] attribute
            var hasAttribute = paramSymbol.GetAttributes()
                .Any(ad => ad.AttributeClass?.Name == "ReadonlyAttribute" || 
                           ad.AttributeClass?.Name == "Readonly");

            if (hasAttribute)
            {
                // Get type name (fully qualified to avoid namespace issues)
                var typeName = paramSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                parametersToGenerate.Add(new ParameterInfo(
                    paramSymbol.Name, 
                    typeName
                ));
            }
        }

        if (parametersToGenerate.Count == 0) return null;

        // Determine namespace
        var namespaceName = symbol.ContainingNamespace.IsGlobalNamespace 
            ? null 
            : symbol.ContainingNamespace.ToDisplayString();

        // Handle nested classes or generics (simplified for this example, usually requires more robust hierarchy building)
        // Getting the full declaration line (e.g., "public partial class MyClass<T>")
        var typeKind = symbol.IsValueType ? "struct" : "class";
        var accessibility = symbol.DeclaredAccessibility.ToString().ToLowerInvariant();
            
        // Construct the type definition name (including generics like MyClass<T>)
        var typeNameWithGenerics = symbol.Name;
        if (symbol.TypeParameters.Length > 0)
        {
            typeNameWithGenerics += "<" + string.Join(", ", symbol.TypeParameters.Select(tp => tp.Name)) + ">";
        }

        return new ClassToGenerate(
            namespaceName, 
            typeDeclaration.Identifier.Text, // File name friendly
            typeNameWithGenerics,            // Code friendly
            accessibility, 
            typeKind, 
            parametersToGenerate);
    }

    private static void Execute(SourceProductionContext context, ClassToGenerate? target)
    {
        if (target is null) return;

        var sb = new StringBuilder();
        var tab = "";
            
        // Add Namespace
        if (!string.IsNullOrEmpty(target.Namespace))
        {
            sb.AppendLine($"namespace {target.Namespace}");
            sb.AppendLine("{");
            tab = "    ";
        }

        // Begin Class/Struct
        // Note: The user must declare their class as 'partial' for this to work
        sb.AppendLine($"{tab}{target.Accessibility} partial {target.TypeKind} {target.TypeName}");
        sb.AppendLine($"{tab}{{");

        // Generate Fields
        foreach (var param in target.Parameters)
        {
            // Convert camelCase param to _camelCase field
            var fieldName = "_" + param.Name;
                
            // Assignment: private readonly int _id = id;
            // This works because field initializers have access to primary constructor parameters.
            sb.AppendLine($"{tab}    private readonly {param.Type} {fieldName} = {param.Name};");
        }

        sb.AppendLine($"{tab}}}");

        if (!string.IsNullOrEmpty(target.Namespace))
        {
            sb.AppendLine("}");
        }

        context.AddSource($"{target.FileName}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    // Helper Records for Data Transfer
    internal record ParameterInfo(string Name, string Type)
    {
        public string Name { get; } = Name;
        public string Type { get; } = Type;
    }

    internal record ClassToGenerate(
        string? Namespace, 
        string FileName, 
        string TypeName, 
        string Accessibility, 
        string TypeKind, 
        List<ParameterInfo> Parameters)
    {
        public string Namespace { get; } = Namespace;
        public string FileName { get; } = FileName;
        public string TypeName { get; } = TypeName;
        public string Accessibility { get; } = Accessibility;
        public string TypeKind { get; } = TypeKind;
        public List<ParameterInfo> Parameters { get; } = Parameters;
    }
}