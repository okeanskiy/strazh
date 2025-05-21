using System.Linq;

namespace PrecisionApi.Domain
{
    public abstract class Node : IInspectable
    {
        public abstract string Label { get; }

        public virtual string FullName { get; set; } = string.Empty;

        public virtual string Name { get; set; } = string.Empty;

        public virtual string Pk { get; protected set; } = string.Empty;

        // Constructor for typical instantiation
        public Node(string fullName, string name)
        {
            FullName = fullName ?? string.Empty;
            Name = name ?? string.Empty;
            SetPrimaryKey();
        }

        // Parameterless constructor for object initializer scenarios
        protected Node()
        {
            // Pk will be set by SetPrimaryKey or re-set by derived classes if needed
            // FullName and Name are initialized to string.Empty by their property initializers
        }

        public virtual void SetPrimaryKey()
        {
            Pk = (FullName ?? string.Empty).GetHashCode().ToString();
        }

        public virtual string Set(string node) => 
            $"{node}.pk = \"{Pk}\", {node}.fullName = \"{FullName}\", {node}.name = \"{Name}\"";

        public string ToInspection() =>
            $$"""{ "Pk": {{(Pk ?? string.Empty).Inspect()}}, "Label": {{(Label ?? string.Empty).Inspect()}}, "FullName": {{(FullName ?? string.Empty).Inspect()}}, "Name": {{(Name ?? string.Empty).Inspect()}} }""";

        public override bool Equals(object? obj) // CS8765 addressed: object? obj
        {
            if (obj is Node otherNode)
            {
                return !string.IsNullOrEmpty(Pk) && Pk == otherNode.Pk;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return (Pk ?? string.Empty).GetHashCode();
        }
    }

    public abstract class CodeNode : Node
    {
        public CodeNode(string fullName, string name, string[]? modifiers = null)
            : base(fullName, name)
        {
            Modifiers = modifiers == null ? string.Empty : string.Join(", ", modifiers);
        }
        protected CodeNode() { Modifiers = string.Empty; } // For initializers

        public string Modifiers { get; protected set; }

        public override string Set(string node)
            => $"{base.Set(node)}{(string.IsNullOrEmpty(Modifiers) ? "" : $", {node}.modifiers = \"{Modifiers}\"")}";
    }

    public abstract class TypeNode : CodeNode
    {
        public TypeNode(string fullName, string name, string[]? modifiers = null)
            : base(fullName, name, modifiers)
        {
        }
        protected TypeNode() { } // For initializers
    }

    public class ClassNode : TypeNode
    {
        public ClassNode(string fullName, string name, string[]? modifiers = null)
            : base(fullName, name, modifiers)
        {
        }
        public ClassNode() { } // For initializers

        public override string Label { get; } = "Class";
    }

    public class InterfaceNode : TypeNode
    {
        public InterfaceNode(string fullName, string name, string[]? modifiers = null)
            : base(fullName, name, modifiers)
        {
        }
        public InterfaceNode() { } // For initializers

        public override string Label { get; } = "Interface";
    }

    public class MethodNode : CodeNode
    {
        public MethodNode(string fullName, string name, (string name, string type)[]? args, string returnType, string[]? modifiers = null)
            : base(fullName, name, modifiers)
        {
            var argStrings = args?.Select(x => $"{x.type} {x.name}") ?? Enumerable.Empty<string>();
            Arguments = string.Join(", ", argStrings);
            ReturnType = returnType ?? string.Empty;
            SetPrimaryKey(); // Recalculate PK with new properties
        }
        public MethodNode() 
        { 
            Arguments = string.Empty; 
            ReturnType = string.Empty; 
        } // For initializers

        public override string Label { get; } = "Method";
        public string Arguments { get; protected set; }
        public string ReturnType { get; protected set; }

        public override string Set(string node)
            => $"{base.Set(node)}, {node}.returnType = \"{ReturnType}\", {node}.arguments = \"{Arguments}\"";

        public override void SetPrimaryKey()
        {
            Pk = $"{(FullName ?? string.Empty)}{(Arguments ?? string.Empty)}{(ReturnType ?? string.Empty)}".GetHashCode().ToString();
        }
    }

    public class FileNode : Node
    {
        public FileNode(string fullName, string name)
            : base(fullName, name) { }
        public FileNode() { } // For initializers

        public override string Label { get; } = "File";
    }

    public class FolderNode : Node
    {
        // Constructor used by AnalysisService object initializer for projectFolderNode
        public FolderNode() : base() { }

        // Standard constructor if direct instantiation with parameters is needed
        public FolderNode(string fullName, string name)
            : base(fullName, name) { }

        public override string Label { get; } = "Folder";

        // Allow Pk to be set directly after FullName and Name are set by initializer
        public void SetPrimaryKey(string pkValue)
        {
            Pk = pkValue ?? string.Empty;
        }
    }

    public class SolutionNode : Node
    {
        public SolutionNode(string name)
            : base(name, name) { } 
        public SolutionNode() { } // For initializers
        public override string Label => "Solution";
    }

    public class ProjectNode : Node
    {
        public ProjectNode(string fullName, string name)
            : base(fullName, name) { }
        public ProjectNode() { } // For initializers

        public override string Label { get; } = "Project";
    }

    public class PackageNode : Node
    {
        public PackageNode(string fullName, string name, string version)
            : base(fullName, name)
        {
            Version = version ?? string.Empty;
            SetPrimaryKey(); // Recalculate PK with new properties
        }
        public PackageNode() { Version = string.Empty; } // For initializers

        public override string Label { get; } = "Package";
        public string Version { get; protected set; }

        public override string Set(string node)
            => $"{base.Set(node)}, {node}.version = \"{Version}\"";

        public override void SetPrimaryKey()
        {
            Pk = $"{(FullName ?? string.Empty)}{(Version ?? string.Empty)}".GetHashCode().ToString();
        }
    }
} 