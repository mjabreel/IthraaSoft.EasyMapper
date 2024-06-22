using IthraaSoft.EasyMapper.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IthraaSoft.EasyMapper;

[Generator]
public class MappingGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register a syntax provider to find classes with MapFrom and/or MapTo attributes

        var targetClassesDeclartions = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => IsItAClassWithSomeAttributes(node),
                transform: (generatorContext, _) => Transform(generatorContext)
            )
            .Where(classWithAttributes => classWithAttributes is not null)
            .Collect();

        // Register the source output

        context.RegisterSourceOutput(targetClassesDeclartions, 
            (spc, targetClasses) => GenerateMappingSourceCode(spc, targetClasses));
    }

    private static bool IsItAClassWithSomeAttributes(SyntaxNode node) =>
        node is not null &&
        node is ClassDeclarationSyntax classDeclaration &&
        classDeclaration.AttributeLists.Count > 0;


    private static ClassWithMappingAttributes Transform(GeneratorSyntaxContext context)
    {
        var classDeclaration = context.Node as ClassDeclarationSyntax;
        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration) is INamedTypeSymbol classSymbol)
        {
            var mapFromAttributesIfAny = classSymbol.GetAttributes()
                .Where(ad => ad.AttributeClass.Name == nameof(MapFromAttribute))
                .ToList();

            var mapToAttributesIfAny = classSymbol.GetAttributes()
                .Where(ad => ad.AttributeClass.Name == nameof (MapToAttribute))
                .ToList();

            if (mapFromAttributesIfAny.Count > 0 || mapToAttributesIfAny.Count > 0)
            {
                return new (classSymbol, mapFromAttributesIfAny, mapToAttributesIfAny);
            }
        }
        return null;
    }

    private static void GenerateMappingSourceCode(SourceProductionContext context, IEnumerable<ClassWithMappingAttributes> targetClassesMetadata)
    {
        var extensionGenerationContexts = new Dictionary<string, ExtensionGenerationContext>();

        foreach (var targetClass in targetClassesMetadata)
        {
            var classSymbol = targetClass.ClassSymbol;

            if (targetClass.MapFromAttributes.Count > 0)
            {
                foreach (var targetAttribute in targetClass.MapFromAttributes)
                {
                    var sourceTypes = targetAttribute.ConstructorArguments[0].Values.Select(v => v.Value as INamedTypeSymbol);
                    foreach (var sourceType in sourceTypes)
                    {
                        if (sourceType is not null)
                        {
                            var pairKey = $"{sourceType.ToDisplayString()}->{classSymbol.ToDisplayString()}";
                            if (!extensionGenerationContexts.ContainsKey(pairKey))
                            {
                                extensionGenerationContexts.Add(pairKey, new ExtensionGenerationContext
                                {
                                    Key = pairKey,
                                    Source = sourceType,
                                    Target = classSymbol,
                                });
                            }
                        }
                    }
                }
            }

            if (targetClass.MapToAttributes.Count > 0)
            {
                foreach (var sourceAttribute in targetClass.MapToAttributes)
                {
                    var targetTypes = sourceAttribute.ConstructorArguments[0].Values.Select(v => v.Value as INamedTypeSymbol);
                    foreach (var targetType in targetTypes)
                    {
                        if (targetType is not null)
                        {
                            var pairKey = $"{classSymbol.ToDisplayString()}->{targetType.ToDisplayString()}";
                            var reversedKey = $"{targetType.ToDisplayString()}->{classSymbol.ToDisplayString()}";


                            if (extensionGenerationContexts.TryGetValue(reversedKey, out var extensionGenerationContext))
                            {
                                extensionGenerationContext.IsBidirectional = extensionGenerationContext.Key != pairKey;
                            }
                            else if (!extensionGenerationContexts.ContainsKey(pairKey))
                            {
                                extensionGenerationContexts.Add(pairKey, new ExtensionGenerationContext
                                {
                                    Key = pairKey,
                                    Source = classSymbol,
                                    Target = targetType,
                                });
                            }
                        }
                    }
                }
            }
        }

        foreach (var extensionContext in extensionGenerationContexts.Values)
        {
            GenerateExtensions(context, extensionContext);
        }
    }

    private static void GenerateExtensions(SourceProductionContext context, ExtensionGenerationContext extensionContext)
    {
        var sourceNamespace = extensionContext.Source.ContainingNamespace.ToDisplayString();
        var targetNamespace = extensionContext.Target.ContainingNamespace.ToDisplayString();
        var sourceName = extensionContext.Source.Name;
        var targetName = extensionContext.Target.Name;

        using var sourceWriter = new StringWriter();
        using var indentedWriter = new IndentedTextWriter(sourceWriter, "    ");
        var usingDirectives = new HashSet<string> { "using System;", $"using {sourceNamespace};", $"using {targetNamespace};" };

        // Write using directives
        foreach (var directive in usingDirectives)
        {
            indentedWriter.WriteLine(directive);
        }

        indentedWriter.WriteLine($"namespace {sourceNamespace}");
        indentedWriter.WriteLine("{");
        indentedWriter.Indent++;
        indentedWriter.WriteLine($"public static class {sourceName}{targetName}MappingExtensions");
        indentedWriter.WriteLine("{");
        indentedWriter.Indent++;


        GenerateToMethod(indentedWriter, extensionContext.Source, extensionContext.Target);
        GenerateFromMethod(indentedWriter, extensionContext.Source, extensionContext.Target);
        if (extensionContext.IsBidirectional)
        {
            GenerateToMethod(indentedWriter, extensionContext.Target, extensionContext.Source);
            GenerateFromMethod(indentedWriter, extensionContext.Target, extensionContext.Source);
        }

        indentedWriter.Indent--;
        indentedWriter.WriteLine("}");
        indentedWriter.Indent--;
        indentedWriter.WriteLine("}");

        context.AddSource($"{sourceName}{targetName}MappingExtensions.g.cs", SourceText.From(sourceWriter.ToString(), Encoding.UTF8));
    }

    private static void GenerateToMethod(IndentedTextWriter writer, INamedTypeSymbol sourceType, INamedTypeSymbol targetType)
    {
        var targetTypeName = targetType.Name;
        var sourceTypeName = sourceType.Name;

        writer.WriteLine($@"public static {targetTypeName} To{targetTypeName}(this {sourceTypeName} source)");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine($"var target = new {targetTypeName}();");

        foreach (var property in sourceType.GetMembers().OfType<IPropertySymbol>())
        {
            if (property.GetAttributes().Any(ad => ad.AttributeClass.Name == nameof(MappingIgnoreAttribute)))
            {
                continue;
            }

            var targetProperty = targetType
                .GetMembers()
                .OfType<IPropertySymbol>()
                .FirstOrDefault(p => p.Name == property.Name);

            if (targetProperty is null)
            {
                var nameAttribute = property
                    .GetAttributes()
                    .FirstOrDefault(ad => ad.AttributeClass.Name == nameof(PropertyNameAttribute));

                if (nameAttribute is not null)
                {
                    var targetPropertyName = nameAttribute.ConstructorArguments[0].Value as string;
                    targetProperty = targetType.GetMembers()
                        .OfType<IPropertySymbol>()
                        .FirstOrDefault(p => p.Name == targetPropertyName);
                }
            }

            if (targetProperty is not null)
            {
                writer.WriteLine($"target.{targetProperty.Name} = source.{property.Name};");
            }
        }

        writer.WriteLine($"return target;");
        writer.Indent--;
        writer.WriteLine("}");
    }

    private static void GenerateFromMethod(IndentedTextWriter writer, INamedTypeSymbol sourceType, INamedTypeSymbol targetType)
    {
        var targetTypeName = targetType.Name;
        var sourceTypeName = sourceType.Name;

        writer.WriteLine($@"public static {targetTypeName} From{sourceTypeName}(this {targetTypeName} target, {sourceTypeName} source)");
        writer.WriteLine("{");
        writer.Indent++;

        foreach (var property in sourceType.GetMembers().OfType<IPropertySymbol>())
        {
            if (property.GetAttributes().Any(ad => ad.AttributeClass.Name == nameof(MappingIgnoreAttribute)))
                continue;

            var targetProperty = targetType
                .GetMembers()
                .OfType<IPropertySymbol>()
                .FirstOrDefault(p => p.Name == property.Name);

            if (targetProperty is null)
            {
                var nameAttribute = property
                    .GetAttributes()
                    .FirstOrDefault(ad => ad.AttributeClass.Name == nameof(PropertyNameAttribute));

                if (nameAttribute is not null)
                {
                    var targetPropertyName = nameAttribute.ConstructorArguments[0].Value as string;
                    targetProperty = targetType
                        .GetMembers()
                        .OfType<IPropertySymbol>()
                        .FirstOrDefault(p => p.Name == targetPropertyName);
                }
            }

            if (targetProperty is not null)
            {
                writer.WriteLine($"target.{targetProperty.Name} = source.{property.Name};");
            }
        }

        writer.WriteLine($"return target;");
        writer.Indent--;
        writer.WriteLine("}");
    }

}
