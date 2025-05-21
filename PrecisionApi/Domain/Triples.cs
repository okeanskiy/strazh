namespace PrecisionApi.Domain
{
    public abstract class Triple : IInspectable
    {
        public Node NodeA { get; set; }

        public Node NodeB { get; set; }

        public Relationship Relationship { get; set; }

        protected Triple(Node nodeA, Node nodeB, Relationship relationship)
        {
            NodeA = nodeA;
            NodeB = nodeB;
            Relationship = relationship;
        }

        // This ToString() is specific to Neo4j Cypher generation and will NOT be used for general purposes in PrecisionApi.
        // We will use a different mechanism for Triple uniqueness if needed (e.g., for deduplication).
        public override string ToString()
            => $"MERGE (a:{NodeA.Label} {{ pk: \"{NodeA.Pk}\" }}) ON CREATE SET {NodeA.Set("a")} ON MATCH SET {NodeA.Set("a")} MERGE (b:{NodeB.Label} {{ pk: \"{NodeB.Pk}\" }}) ON CREATE SET {NodeB.Set("b")} ON MATCH SET {NodeB.Set("b")} MERGE (a)-[:{Relationship.Type}]->(b);";

        public string ToInspection() => 
            $$"""{ "NodeA": {{(NodeA?.ToInspection() ?? "null")}}, "NodeB": {{(NodeB?.ToInspection() ?? "null")}}, "Relationship": {{(Relationship?.ToInspection() ?? "null")}} }""";
    }

    // Structure

    public class TripleDependsOnProject : Triple
    {
        public TripleDependsOnProject(
            ProjectNode projectA,
            ProjectNode projectB)
            : base(projectA, projectB, new DependsOnRelationship())
        { }
    }

    public class TripleDependsOnPackage : Triple
    {
        public TripleDependsOnPackage(
            ProjectNode projectA,
            PackageNode packageB)
            : base(projectA, packageB, new DependsOnRelationship())
        { }
    }

    public class TripleIncludedIn : Triple
    {
        public TripleIncludedIn(
            SolutionNode solution,
            FolderNode folder)
            : base(solution, folder, new IncludedInRelationship())
        {
        }

        public TripleIncludedIn(
            ProjectNode contentA,
            FolderNode contentB)
            : base(contentA, contentB, new IncludedInRelationship())
        { }

        public TripleIncludedIn(
            FolderNode contentA,
            FolderNode contentB)
            : base(contentA, contentB, new IncludedInRelationship())
        { }

        public TripleIncludedIn(
            FileNode contentA,
            FolderNode contentB)
            : base(contentA, contentB, new IncludedInRelationship())
        { }

    }
    
    public class TripleContains : Triple
    {
        public TripleContains(
            SolutionNode solution,
            ProjectNode project)
            : base(solution, project, new ContainsRelationship())
        {
        }
    }

    public class TripleDeclaredAt : Triple
    {
        public TripleDeclaredAt(
            TypeNode typeA,
            FileNode fileB)
            : base(typeA, fileB, new DeclaredAtRelationship())
        { }
    }

    // Code

    public class TripleInvoke : Triple
    {
        public TripleInvoke(
            MethodNode methodA,
            MethodNode methodB)
            : base(methodA, methodB, new InvokeRelationship())
        { }
    }

    public class TripleHave : Triple
    {
        public TripleHave(
            TypeNode typeA,
            MethodNode methodB)
            : base(typeA, methodB, new HaveRelationship())
        { }
    }

    public class TripleConstruct : Triple
    {
        public TripleConstruct(
            MethodNode methodA,
            ClassNode classB)
            : base(methodA, classB, new ConstructRelationship())
        { }
    }

    public class TripleOfType : Triple
    {
        public TripleOfType(
            ClassNode classA,
            TypeNode typeB)
            : base(classA, typeB, new OfTypeRelationship())
        { }

        public TripleOfType(
            InterfaceNode interfaceA,
            InterfaceNode interfaceB)
            : base(interfaceA, interfaceB, new OfTypeRelationship())
        { }
    }
} 