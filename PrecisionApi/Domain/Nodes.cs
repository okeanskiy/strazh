using System.Linq;

namespace PrecisionApi.Domain
{
    public abstract class Node : IInspectable
    {
        public abstract string Label { get; }

        public virtual string FullName { get; }

        public virtual string Name { get; }

        public virtual string Pk { get; protected set; }

        public Node(string fullName, string name)
        {
            FullName = fullName;
            Name = name;
            SetPrimaryKey();
        }

        protected virtual void SetPrimaryKey()
        {
            // Ensure Pk is never null or empty for hash code generation
            Pk = (FullName ?? string.Empty).GetHashCode().ToString();
        }

        public virtual string Set(string node) => 
            $"{node}.pk = \"{Pk}\", {node}.fullName = \"{FullName}\", {node}.name = \"{Name}\"";

        public string ToInspection() =>
            $$"""{ "Pk": {{(Pk ?? string.Empty).Inspect()}}, "Label": {{(Label ?? string.Empty).Inspect()}}, "FullName": {{(FullName ?? string.Empty).Inspect()}}, "Name": {{(Name ?? string.Empty).Inspect()}} }""";

        // For use in HashSet/Dictionary to ensure uniqueness based on Pk
        public override bool Equals(object obj)
        {
            if (obj is Node otherNode)
            {
                return Pk == otherNode.Pk;
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
        public CodeNode(string fullName, string name, string[] modifiers = null)
            : base(fullName, name)
        {
            Modifiers = modifiers == null ? string.Empty : string.Join(", ", modifiers);
        }

        public string Modifiers { get; }

        public override string Set(string node)
            => $"{base.Set(node)}{(string.IsNullOrEmpty(Modifiers) ? "" : $", {node}.modifiers = \"{Modifiers}\"")}";
    }

    public abstract class TypeNode : CodeNode
    {
        public TypeNode(string fullName, string name, string[] modifiers = null)
            : base(fullName, name, modifiers)
        {
        }
    }

    public class ClassNode : TypeNode
    {
        public ClassNode(string fullName, string name, string[] modifiers = null)
            : base(fullName, name, modifiers)
        {
        }

        public override string Label { get; } = "Class";
    }

    public class InterfaceNode : TypeNode
    {
        public InterfaceNode(string fullName, string name, string[] modifiers = null)
            : base(fullName, name, modifiers)
        {
        }

        public override string Label { get; } = "Interface";
    }

    public class MethodNode : CodeNode
    {
        public MethodNode(string fullName, string name, (string name, string type)[] args, string returnType, string[] modifiers = null)
            : base(fullName, name, modifiers)
        {
            var argStrings = args?.Select(x => $"{x.type} {x.name}") ?? Enumerable.Empty<string>();
            Arguments = string.Join(", ", argStrings);
            ReturnType = returnType ?? string.Empty;
            SetPrimaryKey(); // Recalculate PK with new properties
        }

        public override string Label { get; } = "Method";
        public string Arguments { get; }
        public string ReturnType { get; }

        public override string Set(string node)
            => $"{base.Set(node)}, {node}.returnType = \"{ReturnType}\", {node}.arguments = \"{Arguments}\"";

        protected override void SetPrimaryKey()
        {
            Pk = $"{(FullName ?? string.Empty)}{(Arguments ?? string.Empty)}{(ReturnType ?? string.Empty)}".GetHashCode().ToString();
        }
    }

    public class FileNode : Node
    {
        public FileNode(string fullName, string name)
            : base(fullName, name) { }

        public override string Label { get; } = "File";
    }

    public class FolderNode : Node
    {
        public FolderNode(string fullName, string name)
            : base(fullName, name) { }

        public override string Label { get; } = "Folder";
    }

    public class SolutionNode : Node
    {
        public SolutionNode(string name)
            : base(name, name) { } // FullName and Name are the same for Solution
        public override string Label => "Solution";
    }

    public class ProjectNode : Node
    {
        public ProjectNode(string name)
            : this(name, name) { } // Often FullName and Name are the same for Project initially

        public ProjectNode(string fullName, string name)
            : base(fullName, name) { }

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

        public override string Label { get; } = "Package";
        public string Version { get; }

        public override string Set(string node)
            => $"{base.Set(node)}, {node}.version = \"{Version}\"";

        protected override void SetPrimaryKey()
        {
            Pk = $"{(FullName ?? string.Empty)}{(Version ?? string.Empty)}".GetHashCode().ToString();
        }
    }
} 