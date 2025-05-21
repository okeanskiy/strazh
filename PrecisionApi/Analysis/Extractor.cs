using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using PrecisionApi.Domain; // Adjusted to new Domain namespace
using System.IO;

namespace PrecisionApi.Analysis // Adjusted namespace
{
    public static class Extractor
    {
        private static TypeNode CreateTypeNode(this ISymbol symbol, TypeDeclarationSyntax declaration)
        {
            // Ensure symbol and its components are not null before accessing properties
            if (symbol == null || symbol.ContainingNamespace == null || declaration == null)
                return null;

            (string fullName, string name) = (symbol.ContainingNamespace.ToString() + '.' + symbol.Name, symbol.Name);
            switch (declaration)
            {
                case ClassDeclarationSyntax _:
                    return new ClassNode(fullName, name, declaration.Modifiers.MapModifiers());
                case InterfaceDeclarationSyntax _:
                    return new InterfaceNode(fullName, name, declaration.Modifiers.MapModifiers());
            }
            return null;
        }

        private static ClassNode CreateClassNode(this TypeInfo typeInfo)
        {
            if (typeInfo.Type == null || typeInfo.Type.ContainingNamespace == null) return null;
            return new ClassNode(GetFullName(typeInfo), GetName(typeInfo));
        }
        
        private static InterfaceNode CreateInterfaceNode(this TypeInfo typeInfo)
        {
            if (typeInfo.Type == null || typeInfo.Type.ContainingNamespace == null) return null;
            return new InterfaceNode(GetFullName(typeInfo), GetName(typeInfo));
        }

        private static string[] MapModifiers(this SyntaxTokenList syntaxTokens)
            => syntaxTokens.Select(x => x.ValueText).ToArray();

        private static TypeNode CreateTypeNode(this TypeInfo typeInfo)
        {
            if (typeInfo.ConvertedType == null) return null; 

            switch (typeInfo.ConvertedType.TypeKind)
            {
                case TypeKind.Interface:
                    return CreateInterfaceNode(typeInfo);

                case TypeKind.Class:
                    return CreateClassNode(typeInfo);

                default:
                    return null;
            }
        }

        private static string GetName(this TypeInfo typeInfo)
            => typeInfo.Type?.Name ?? string.Empty;

        private static string GetFullName(this TypeInfo typeInfo)
            => (typeInfo.Type?.ContainingNamespace?.ToString() ?? string.Empty) + "." + GetName(typeInfo);
        
        private static string GetFullNamespaceString(INamespaceSymbol? ns)
        {
            if (ns == null || ns.IsGlobalNamespace) 
                return string.Empty;
            
            string parentNs = GetFullNamespaceString(ns.ContainingNamespace);
            return string.IsNullOrEmpty(parentNs) ? ns.Name : $"{parentNs}.{ns.Name}";
        }

        private static MethodNode CreateMethodNode(this IMethodSymbol symbol, MethodDeclarationSyntax? declaration = null)
        {
            if (symbol == null || symbol.ContainingType == null || symbol.ReturnType == null) // Removed symbol.ContainingNamespace check as GetFullNamespaceString handles global
                return null;

            string namespaceStr = GetFullNamespaceString(symbol.ContainingNamespace);
            string typeName = symbol.ContainingType.Name ?? string.Empty;
            string methodName = symbol.Name ?? string.Empty;

            if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(methodName))
                return null; // Cannot form a valid name

            var fullName = string.IsNullOrEmpty(namespaceStr) ? 
                           $"{typeName}.{methodName}" : 
                           $"{namespaceStr}.{typeName}.{methodName}";
            
            var args = symbol.Parameters.Select(x => (name: x.Name ?? string.Empty, type: x.Type?.ToString() ?? "unknown_type")).ToArray();
            var returnType = symbol.ReturnType.ToString() ?? string.Empty; // Ensure returnType is not null
            
            // Ensure fullName and methodName (used as 'name' parameter for Node constructor) are not null
            // The Node constructor now handles nulls with ?? string.Empty, but good practice here.
            fullName = fullName ?? string.Empty;
            methodName = methodName ?? string.Empty;

            return new MethodNode(fullName,
                methodName, // Use the non-null methodName
                args,
                returnType,
                declaration?.Modifiers.MapModifiers());
        }

        private static string GetName(string filePath)
            => !string.IsNullOrEmpty(filePath) ? filePath.Split(Path.DirectorySeparatorChar).LastOrDefault() ?? string.Empty : string.Empty;

        private static List<TripleIncludedIn> GetFolderChain(string filePath, FileNode file, FolderNode rootFolderProvided)
        {
            var triples = new List<TripleIncludedIn>();
            if (string.IsNullOrEmpty(filePath) || file == null || rootFolderProvided == null || string.IsNullOrEmpty(rootFolderProvided.FullName))
            {
                return triples;
            }

            // Normalize paths to ensure consistent separator usage and avoid trailing separators
            string normalizedFilePath = Path.GetFullPath(filePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedRootPath = Path.GetFullPath(rootFolderProvided.FullName).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Ensure the file path is actually under the root folder path
            if (!normalizedFilePath.StartsWith(normalizedRootPath, StringComparison.OrdinalIgnoreCase))
            {
                // If not under the root, perhaps just link file to the root and return
                // Or handle as an error/log, depending on desired behavior. For now, link to root if different.
                if (file.FullName != rootFolderProvided.FullName) // Avoid self-reference if file is the root
                   // triples.Add(new TripleIncludedIn(file, rootFolderProvided)); This was causing issues
                return triples; // Or decide on a different strategy
            }

            var relativePath = Path.GetRelativePath(normalizedRootPath, normalizedFilePath);
            string[] pathSegments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            FolderNode currentParentFolder = rootFolderProvided;
            string currentCumulativePath = normalizedRootPath;

            // Iterate through folder segments, creating FolderNodes and TripleIncludedIn relationships
            // Skip the last segment if it's the file name itself
            for (int i = 0; i < pathSegments.Length - 1; i++)
            {
                string segmentName = pathSegments[i];
                if (string.IsNullOrEmpty(segmentName)) continue;

                currentCumulativePath = Path.Combine(currentCumulativePath, segmentName);
                var folderNode = new FolderNode(currentCumulativePath, segmentName);
                triples.Add(new TripleIncludedIn(folderNode, currentParentFolder));
                currentParentFolder = folderNode;
            }

            // Link the file itself to the last identified parent folder
            triples.Add(new TripleIncludedIn(file, currentParentFolder));
            
            return triples;
        }

        public static void AnalyzeTree(IList<Triple> triples, SyntaxTree st, SemanticModel sem, FolderNode rootProjectFolder)
        {
            if (st == null || sem == null || triples == null || rootProjectFolder == null)
                return;

            var rootSyntaxNode = st.GetRoot();
            if (rootSyntaxNode == null) return;

            var filePath = st.FilePath;
            if (string.IsNullOrEmpty(filePath)) return; // Cannot process if no file path

            var fileName = GetName(filePath);
            var fileNode = new FileNode(filePath, fileName);
            
            var folderTriples = GetFolderChain(filePath, fileNode, rootProjectFolder);
            foreach(var triple in folderTriples) { triples.Add(triple); }
            
            var declarations = rootSyntaxNode.DescendantNodes().OfType<TypeDeclarationSyntax>();
            foreach (var declaration in declarations)
            {
                var symbol = sem.GetDeclaredSymbol(declaration);
                if (symbol == null) continue;

                var typeNode = symbol.CreateTypeNode(declaration);
                if (typeNode != null)
                {
                    triples.Add(new TripleDeclaredAt(typeNode, fileNode));
                    GetInherits(triples, declaration, sem, typeNode);
                    GetMethodsAll(triples, declaration, sem, typeNode, fileNode); // Pass fileNode for context if needed
                }
            }
        }

        public static void GetInherits(IList<Triple> triples, TypeDeclarationSyntax declaration, SemanticModel sem, TypeNode node)
        {
            if (declaration?.BaseList == null || triples == null || sem == null || node == null)
                return;

            foreach (var baseTypeSyntax in declaration.BaseList.Types)
            {
                if (baseTypeSyntax.Type == null) continue;
                var typeInfo = sem.GetTypeInfo(baseTypeSyntax.Type);
                var parentNode = typeInfo.CreateTypeNode(); // Uses the extension method on TypeInfo
                if (parentNode != null)
                {
                    if (node is ClassNode classNode)
                    {
                        triples.Add(new TripleOfType(classNode, parentNode));
                    }
                    else if (node is InterfaceNode interfaceNode && parentNode is InterfaceNode parentInterfaceNode) // Ensure parent is also an interface for I->I
                    {
                        triples.Add(new TripleOfType(interfaceNode, parentInterfaceNode));
                    }
                }
            }
        }

        public static void GetMethodsAll(IList<Triple> triples, TypeDeclarationSyntax declaration, SemanticModel sem, TypeNode typeNode, FileNode fileNode)
        {
            if (declaration == null || triples == null || sem == null || typeNode == null)
                return;

            var methods = declaration.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var methodSyntax in methods)
            {
                var methodSymbol = sem.GetDeclaredSymbol(methodSyntax);
                if (methodSymbol == null) continue;

                var methodNode = methodSymbol.CreateMethodNode(methodSyntax);
                if (methodNode == null) continue; 

                triples.Add(new TripleHave(typeNode, methodNode));

                // Analyze method body for invocations and constructions
                foreach (var expressionSyntax in methodSyntax.DescendantNodes().OfType<ExpressionSyntax>())
                {
                    switch (expressionSyntax)
                    {
                        case ObjectCreationExpressionSyntax creationSyntax:
                            var createdTypeInfo = sem.GetTypeInfo(creationSyntax.Type);
                            // We are interested in classes being constructed
                            if (createdTypeInfo.Type?.TypeKind == TypeKind.Class)
                            {
                                var classBeingConstructedNode = createdTypeInfo.CreateClassNode(); 
                                if (classBeingConstructedNode != null)
                                {
                                    triples.Add(new TripleConstruct(methodNode, classBeingConstructedNode));
                                }
                            }
                            break;

                        case InvocationExpressionSyntax invocationSyntax:
                            var invokedSymbolInfo = sem.GetSymbolInfo(invocationSyntax.Expression);
                            if (invokedSymbolInfo.Symbol is IMethodSymbol invokedMethodSymbol)
                            {
                                var invokedMethodNode = invokedMethodSymbol.CreateMethodNode(); // Pass corresponding MethodDeclarationSyntax if available and needed
                                if (invokedMethodNode != null)
                                {
                                    triples.Add(new TripleInvoke(methodNode, invokedMethodNode));
                                }
                            }
                            break;
                    }
                }
            }
        }
    }
} 