using System.Linq;

namespace Strazh.Domain
{
    public abstract class Node : IInspectable
    {
        public abstract string Label { get; }

        public virtual string FullName { get; }

        public virtual string Name { get; }

        /// <summary>
        /// Primary Key used to compare Matching of nodes on MERGE operation
        /// </summary>
        public virtual string Pk { get; protected set; }

        public Node(string fullName, string name)
        {
            FullName = fullName;
            Name = name;
            SetPrimaryKey();
        }

        protected virtual void SetPrimaryKey()
        {
            Pk = FullName.GetHashCode().ToString();
        }

        public virtual string Set(string node) => 
            $"{node}.pk = \"{Pk}\", {node}.fullName = \"{FullName}\", {node}.name = \"{Name}\"";

        public string ToInspection() =>
            $$"""{ "Pk": {{Pk.Inspect()}}, "Label": {{Label.Inspect()}}, "FullName": {{FullName.Inspect()}}, "Name": {{Name.Inspect()}} }""";
    }

    // Code

    public abstract class CodeNode : Node
    {
        public CodeNode(string fullName, string name, string[] modifiers = null)
            : base(fullName, name)
        {

            Modifiers = modifiers == null ? "" : string.Join(", ", modifiers);
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
            Arguments = string.Join(", ", args.Select(x => $"{x.type} {x.name}"));
            ReturnType = returnType;
            SetPrimaryKey();
        }

        public override string Label { get; } = "Method";

        public string Arguments { get; }

        public string ReturnType { get; }

        public override string Set(string node)
            => $"{base.Set(node)}, {node}.returnType = \"{ReturnType}\", {node}.arguments = \"{Arguments}\"";

        protected override void SetPrimaryKey()
        {
            Pk = $"{FullName}{Arguments}{ReturnType}".GetHashCode().ToString();
        }
    }

    // Structure

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

    public class SolutionNode(string name) : Node(name, name)
    {
        public override string Label => "Solution";
    }

    public class ProjectNode : Node
    {
        public ProjectNode(string name)
            : this(name, name) { }

        public ProjectNode(string fullName, string name)
            : base(fullName, name) { }

        public override string Label { get; } = "Project";
    }

    public class PackageNode : Node
    {
        public PackageNode(string fullName, string name, string version)
            : base(fullName, name)
        {
            Version = version;
            SetPrimaryKey();
        }

        public override string Label { get; } = "Package";

        public string Version { get; }

        public override string Set(string node)
            => $"{base.Set(node)}, {node}.version = \"{Version}\"";

        protected override void SetPrimaryKey()
        {
            Pk = $"{FullName}{Version}".GetHashCode().ToString();
        }
    }
}