## PrecisionApi.Analysis Namespace

**Purpose:** Contains classes responsible for performing the detailed code analysis using Roslyn, extracting semantic and syntactic information to populate the domain models.

### Files:

*   **`Extractor.cs`**
    *   **Source:** Adapted from `Strazh.Analysis.Extractor`.
    *   **Functionality:** A static class with extension methods and analysis routines that traverse Roslyn `SyntaxTree`s and use `SemanticModel`s to identify code elements and their relationships.
    *   **Key Methods:**
        *   `AnalyzeTree<T>(IList<Triple> triples, SyntaxTree st, SemanticModel sem, FolderNode rootProjectFolder)`: The main entry point for analyzing a syntax tree for type declarations (classes, interfaces). It orchestrates calls to other methods to find details.
            *   Creates `FileNode` and `TripleIncludedIn` for folder structure via `GetFolderChain`.
            *   For each type declaration, it creates a `TypeNode` (e.g., `ClassNode`, `InterfaceNode`) and a `TripleDeclaredAt`.
            *   Calls `GetInherits` and `GetMethodsAll`.
        *   `GetInherits(...)`: Finds base types/interfaces and creates `TripleOfType` relationships.
        *   `GetMethodsAll(...)`: Finds methods within a type, creates `MethodNode`s and `TripleHave` relationships. It then inspects method bodies for:
            *   `ObjectCreationExpressionSyntax`: Creates `TripleConstruct` relationships.
            *   `InvocationExpressionSyntax`: Creates `TripleInvoke` relationships.
        *   `CreateTypeNode`, `CreateClassNode`, `CreateInterfaceNode`, `CreateMethodNode`: Helper extension methods to instantiate domain model `Node` objects from Roslyn symbols (`ISymbol`, `TypeInfo`) and syntax nodes.
        *   `GetFolderChain(...)`: Builds the chain of `TripleIncludedIn` relationships from a file up to its project's root folder.
    *   **Robustness:** The copied version in `PrecisionApi.Analysis` includes numerous null checks and safeguards to handle various code structures and potentially incomplete semantic information from Roslyn more gracefully.
    *   **Dependencies:** `Microsoft.CodeAnalysis`, `PrecisionApi.Domain`. 