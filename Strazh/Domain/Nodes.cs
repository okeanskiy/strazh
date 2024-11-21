using System.Collections.Generic;
using System.Linq;

namespace Strazh.Domain
{
    public abstract class Node
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

        public virtual string Set(string node)
            => $"{node}.pk = \"{Pk}\", {node}.fullName = \"{FullName}\", {node}.name = \"{Name}\"";

        public virtual IDictionary<string, object> GetPropertiesDictionary()
        {
            return new Dictionary<string, object>
            {
                { "pk", Pk },
                { "fullName", FullName },
                { "name", Name }
            };
        }
    }

    public abstract class CodeNode : Node
    {
        public CodeNode(string fullName, string name, string[] modifiers = null, string sourceCode = null)
            : base(fullName, name)
        {
            Modifiers = modifiers == null ? "" : string.Join(", ", modifiers);
            SourceCode = sourceCode;
        }

        public string Modifiers { get; }

        public string SourceCode { get; }

        public override string Set(string node)
            => $"{base.Set(node)}" +
               $"{(string.IsNullOrEmpty(Modifiers) ? "" : $", {node}.modifiers = \"{Modifiers}\"")}" +
               $"{(string.IsNullOrEmpty(SourceCode) ? "" : $", {node}.sourceCode = \"{SourceCode}\"")}";

        public override IDictionary<string, object> GetPropertiesDictionary()
        {
            var properties = base.GetPropertiesDictionary();
            properties.Add("modifiers", Modifiers);
            properties.Add("sourceCode", SourceCode);
            return properties;
        }
    }

    public abstract class TypeNode : CodeNode
    {
        public TypeNode(string fullName, string name, string[] modifiers = null, string sourceCode = null)
            : base(fullName, name, modifiers, sourceCode)
        {
        }
    }

    public class ClassNode : TypeNode
    {
        public ClassNode(string fullName, string name, string[] modifiers = null, string sourceCode = null)
            : base(fullName, name, modifiers, sourceCode)
        {
        }

        public override string Label { get; } = "Class";
    }

    public class InterfaceNode : TypeNode
    {
        public InterfaceNode(string fullName, string name, string[] modifiers = null, string sourceCode = null)
            : base(fullName, name, modifiers, sourceCode)
        {
        }

        public override string Label { get; } = "Interface";
    }

    public class MethodNode : CodeNode
    {
        public MethodNode(string fullName, string name, (string name, string type)[] args, string returnType, string[] modifiers = null, string sourceCode = null)
            : base(fullName, name, modifiers, sourceCode)
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

        public override IDictionary<string, object> GetPropertiesDictionary()
        {
            var properties = base.GetPropertiesDictionary();
            properties.Add("returnType", ReturnType);
            properties.Add("arguments", Arguments);
            return properties;
        }

        protected override void SetPrimaryKey()
        {
            Pk = $"{FullName}{Arguments}{ReturnType}".GetHashCode().ToString();
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