﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Diagnostics;

namespace DemoSourceGenerator;

[Generator]
public class DemoSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var enumTypes = context.SyntaxProvider
            .CreateSyntaxProvider(CouldBeEnumerationAsync, GetEnumTypeOrNull)
            .Where(type => type != null)
            .Collect();

        context.RegisterSourceOutput(enumTypes, GenerateCode!);
    }

    private static bool CouldBeEnumerationAsync(
       SyntaxNode node,
       CancellationToken cancellationToken)
    {
        if (node is not AttributeSyntax attribute)
            return false;

        var name = ExtractName(attribute.Name);

        return name is "HelloWorlded" or "HelloWorldedAttribute";
    }

    private static string? ExtractName(NameSyntax? name)
    {
        return name switch
        {
            SimpleNameSyntax ins => ins.Identifier.Text,
            QualifiedNameSyntax qns => qns.Right.Identifier.Text,
            _ => null
        };
    }

    private static ITypeSymbol? GetEnumTypeOrNull(
       GeneratorSyntaxContext context,
       CancellationToken cancellationToken)
    {
        var attributeSyntax = (AttributeSyntax)context.Node;

        // "attribute.Parent" is "AttributeListSyntax"
        // "attribute.Parent.Parent" is a C# fragment the attributes are applied to
        if (attributeSyntax.Parent?.Parent is not ClassDeclarationSyntax classDeclaration)
            return null;

        var type = context.SemanticModel.GetDeclaredSymbol(classDeclaration) as ITypeSymbol;

        return type is null || !IsEnumeration(type) ? null : type;
    }

    private static bool IsEnumeration(ISymbol type)
    {
        return type.GetAttributes()
                   .Any(a => a.AttributeClass?.Name == "HelloWorldedAttribute" &&
                             a.AttributeClass.ContainingNamespace is
                             {
                                 Name: "Library",
                                 ContainingNamespace.IsGlobalNamespace: true
                             });
    }

    private static void GenerateCode(
         SourceProductionContext context,
         ImmutableArray<ITypeSymbol> enumerations)
    {
        if (enumerations.IsDefaultOrEmpty)
            return;

        foreach (var type in enumerations)
        {
            var code = GenerateCode(type);
            var typeNamespace = type.ContainingNamespace.IsGlobalNamespace
                   ? null
                   : $"{type.ContainingNamespace}.";

            context.AddSource($"{typeNamespace}{type.Name}.hw.g.cs", code);
        }
    }


    private static string GenerateCode(ITypeSymbol type)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace
              ? null
              : type.ContainingNamespace.ToString();
        var name = type.Name;

        return @$"

// <auto-generated />

using System;
namespace {ns};
public static partial class {name}
{{
    public static void HelloWorld()
    {{
        Console.WriteLine(""HelloWorld"");
    }}
}}
";
    
    }
}
