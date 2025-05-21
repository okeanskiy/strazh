## PrecisionApi.Domain Namespace

**Purpose:** Contains the core data model classes representing the code graph's nodes, relationships, and the triples that connect them. This code is largely adapted from the original Strazh project.

### Files:

*   **`IInspectable.cs`**
    *   Defines the `IInspectable` interface with a `ToInspection()` method, originally intended for generating specific string representations (e.g., for logging or debugging in Strazh).
    *   Includes `InspectableExtensions` with an `Inspect()` extension method for strings, used by `ToInspection()` to escape characters.
    *   While `ToInspection()` itself might not be directly used for the final JSON output, the pattern of having structured data representation is maintained.

*   **`Nodes.cs`**
    *   Defines the abstract base class `Node` and various concrete node types that represent elements in a codebase.
    *   **Base `Node`:** `Label`, `FullName`, `Name`, `Pk` (Primary Key). Includes `Equals()` and `GetHashCode()` overrides based on `Pk` for unique identification in collections.
    *   **`CodeNode` (abstract, inherits `Node`):** Adds `Modifiers`.
        *   **`TypeNode` (abstract, inherits `CodeNode`):** Base for class/interface.
            *   `ClassNode`: Represents a class (Label: "Class").
            *   `InterfaceNode`: Represents an interface (Label: "Interface").
        *   `MethodNode` (inherits `CodeNode`): Represents a method. Adds `Arguments`, `ReturnType`. `Pk` is based on `FullName`, `Arguments`, and `ReturnType`.
    *   **Structure Nodes (inherit `Node`):**
        *   `FileNode`: Represents a source file (Label: "File").
        *   `FolderNode`: Represents a folder (Label: "Folder").
        *   `SolutionNode`: Represents a .sln file (Label: "Solution").
        *   `ProjectNode`: Represents a .csproj file (Label: "Project").
        *   `PackageNode`: Represents a NuGet package dependency. Adds `Version`. `Pk` is based on `FullName` and `Version`.
    *   Each node type has a `Label` property used for the JSON output.
    *   The `Pk` property is used as the `id` in the JSON output for nodes.
    *   Constructors and `SetPrimaryKey()` methods are designed to be robust, handling potential nulls and ensuring `Pk` is calculated correctly.

*   **`Relationships.cs`**
    *   Defines the abstract base class `Relationship` and concrete relationship types.
    *   **Base `Relationship`:** Has an abstract `Type` string property.
    *   **Concrete Relationship Types:** (e.g., `HaveRelationship`, `InvokeRelationship`, `DependsOnRelationship`, `IncludedInRelationship`, `ContainsRelationship`, `DeclaredAtRelationship`, `ConstructRelationship`, `OfTypeRelationship`). Each overrides `Type` to provide a specific string like "HAVE", "INVOKE", etc.
    *   The `Type` property is used for the `type` field in the JSON output for edges.

*   **`Triples.cs`**
    *   Defines the abstract base class `Triple` and various concrete triple types.
    *   **Base `Triple`:** Contains `NodeA` (source), `NodeB` (target), and a `Relationship` object.
    *   The original `ToString()` method (generating Cypher for Neo4j) is preserved but commented as not for general use in PrecisionApi, especially not for deduplication.
    *   **Concrete Triple Types:** (e.g., `TripleDependsOnProject`, `TripleInvoke`, `TripleIncludedIn`). These classes link specific `Node` types with specific `Relationship` types.
    *   Triples are the source for generating the "edges" array in the final JSON output, using `NodeA.Pk` as source, `NodeB.Pk` as target, and `Relationship.Type` as the edge type. 