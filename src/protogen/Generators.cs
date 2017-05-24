﻿using Google.Protobuf.Reflection;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Google.Protobuf.Reflection
{
#pragma warning disable CS1591
    partial class FileDescriptorProto
    {
        public void GenerateCSharp(TextWriter target, NameNormalizer normalizer = null, IList<Error> errors = null)
            => Generators.GenerateCSharp(target, this, normalizer, errors);

        public string GenerateCSharp(NameNormalizer normalizer = null, IList<Error> errors = null)
        {
            using (var sw = new StringWriter())
            {
                GenerateCSharp(sw, normalizer, errors);
                return sw.ToString();
            }
        }
    }

#pragma warning restore CS1591
}

namespace ProtoBuf
{

    internal class ParserException : Exception
    {
        public int ColumnNumber { get; }
        public int LineNumber { get; }
        public string Text { get; }
        public string LineContents { get; }
        public bool IsError { get; }
        internal ParserException(Token token, string message, bool isError)
            : base(message ?? "error")
        {
            ColumnNumber = token.ColumnNumber;
            LineNumber = token.LineNumber;
            LineContents = token.LineContents;
            Text = token.Value ?? "";
            IsError = isError;
        }
    }

    public abstract class NameNormalizer
    {
        private class NullNormalizer : NameNormalizer
        {
            protected override string GetName(string identifier) => identifier;
            public override string Pluralize(string identifier) => identifier;
        }
        private class DefaultNormalizer : NameNormalizer
        {
            protected override string GetName(string identifier) => AutoCapitalize(identifier);
            public override string Pluralize(string identifier) => AutoPluralize(identifier);
        }
        protected static string AutoCapitalize(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return identifier;
            // if all upper-case, make proper-case
            if (Regex.IsMatch(identifier, @"^[_A-Z0-9]*$"))
            {
                return Regex.Replace(identifier, @"(^|_)([A-Z0-9])([A-Z0-9]*)",
                    match => match.Groups[2].Value.ToUpperInvariant() + match.Groups[3].Value.ToLowerInvariant());
            }
            // if all lower-case, make proper case
            if (Regex.IsMatch(identifier, @"^[_a-z0-9]*$"))
            {
                return Regex.Replace(identifier, @"(^|_)([a-z0-9])([a-z0-9]*)",
                    match => match.Groups[2].Value.ToUpperInvariant() + match.Groups[3].Value.ToLowerInvariant());
            }
            // just remove underscores - leave their chosen casing alone
            return identifier.Replace("_", "");
        }
        protected static string AutoPluralize(string identifier)
        {
            // horribly Anglo-centric and only covers common cases; but: is swappable

            if (string.IsNullOrEmpty(identifier) || identifier.Length == 1) return identifier;

            if (identifier.EndsWith("ss") || identifier.EndsWith("o")) return identifier + "es";
            if (identifier.EndsWith("is") && identifier.Length > 2) return identifier.Substring(0, identifier.Length - 2) + "es";

            if (identifier.EndsWith("s")) return identifier; // misses some things (bus => buses), but: might already be pluralized

            if (identifier.EndsWith("y") && identifier.Length > 2)
            {   // identity => identities etc
                switch (identifier[identifier.Length - 2])
                {
                    case 'a':
                    case 'e':
                    case 'i':
                    case 'o':
                    case 'u':
                        break; // only for consonant prefix
                    default:
                        return identifier.Substring(0, identifier.Length - 1) + "ies";
                }
            }
            return identifier + "s";
        }
        public static NameNormalizer Default { get; } = new DefaultNormalizer();
        public static NameNormalizer Null { get; } = new NullNormalizer();
        protected abstract string GetName(string identifier);
        public abstract string Pluralize(string identifier);
        public virtual string GetName(DescriptorProto definition)
            => GetName(definition.Parent, GetName(definition.Name), definition.Name, false);
        public virtual string GetName(EnumDescriptorProto definition)
            => GetName(definition.Parent, GetName(definition.Name), definition.Name, false);
        public virtual string GetName(EnumValueDescriptorProto definition) => AutoCapitalize(definition.Name);
        public virtual string GetName(FieldDescriptorProto definition)
        {
            var preferred = GetName(definition.Name);
            if (definition.label == FieldDescriptorProto.Label.LabelRepeated)
            {
                preferred = Pluralize(preferred);
            }
            return GetName(definition.Parent, preferred, definition.Name, true);
        }

        protected HashSet<string> BuildConflicts(DescriptorProto parent, bool includeDescendents)
        {
            var conflicts = new HashSet<string>();
            if (parent != null)
            {
                conflicts.Add(GetName(parent));
                if (includeDescendents)
                {
                    foreach (var type in parent.NestedTypes)
                    {
                        conflicts.Add(GetName(type));
                    }
                    foreach (var type in parent.EnumTypes)
                    {
                        conflicts.Add(GetName(type));
                    }
                }
            }
            return conflicts;
        }
        protected virtual string GetName(DescriptorProto parent, string preferred, string fallback, bool includeDescendents)
        {
            var conflicts = BuildConflicts(parent, includeDescendents);

            if (!conflicts.Contains(preferred)) return preferred;
            if (!conflicts.Contains(fallback)) return fallback;

            var attempt = preferred + "Value";
            if (!conflicts.Contains(attempt)) return attempt;

            attempt = fallback + "Value";
            if (!conflicts.Contains(attempt)) return attempt;

            int i = 1;
            while (true)
            {
                attempt = preferred + i.ToString();
                if (!conflicts.Contains(attempt)) return attempt;
            }
        }
    }
    internal static class Generators
    {
        private static string Escape(string identifier)
        {
            switch (identifier)
            {
                case "abstract":
                case "event":
                case "new":
                case "struct":
                case "as":
                case "explicit":
                case "null":
                case "switch":
                case "base":
                case "extern":
                case "object":
                case "this":
                case "bool":
                case "false":
                case "operator":
                case "throw":
                case "break":
                case "finally":
                case "out":
                case "true":
                case "byte":
                case "fixed":
                case "override":
                case "try":
                case "case":
                case "float":
                case "params":
                case "typeof":
                case "catch":
                case "for":
                case "private":
                case "uint":
                case "char":
                case "foreach":
                case "protected":
                case "ulong":
                case "checked":
                case "goto":
                case "public":
                case "unchecked":
                case "class":
                case "if":
                case "readonly":
                case "unsafe":
                case "const":
                case "implicit":
                case "ref":
                case "ushort":
                case "continue":
                case "in":
                case "return":
                case "using":
                case "decimal":
                case "int":
                case "sbyte":
                case "virtual":
                case "default":
                case "interface":
                case "sealed":
                case "volatile":
                case "delegate":
                case "internal":
                case "short":
                case "void":
                case "do":
                case "is":
                case "sizeof":
                case "while":
                case "double":
                case "lock":
                case "stackalloc":
                case "else":
                case "long":
                case "static":
                case "enum":
                case "namespace":
                case "string":
                    return "@" + identifier;
                default:
                    return identifier;
            }
        }
        private static void Write(GeneratorContext context, EnumDescriptorProto @enum)
        {
            var name = context.Normalizer.GetName(@enum);
            context.WriteLine($@"[global::ProtoBuf.ProtoContract(Name = @""{@enum.Name}"")]");
            WriteOptions(context, @enum.Options);
            context.WriteLine($"public enum {Escape(name)}").WriteLine("{").Indent();
            foreach (var val in @enum.Values)
            {
                name = context.Normalizer.GetName(val);
                context.WriteLine($@"[global::ProtoBuf.ProtoEnum(Name = @""{val.Name}"", Value = {val.Number})]");
                WriteOptions(context, val.Options);
                context.WriteLine($"{Escape(name)} = {val.Number},");
            }
            context.Outdent().WriteLine("}").WriteLine();
        }
        private class GeneratorContext
        {
            public string Syntax => string.IsNullOrWhiteSpace(fileDescriptor.Syntax)
                ? FileDescriptorProto.SyntaxProto2 : fileDescriptor.Syntax;

            public string GetTypeName(FieldDescriptorProto field, out string dataFormat)
            {
                dataFormat = "";
                switch (field.type)
                {
                    case FieldDescriptorProto.Type.TypeDouble:
                        return "double";
                    case FieldDescriptorProto.Type.TypeFloat:
                        return "float";
                    case FieldDescriptorProto.Type.TypeBool:
                        return "bool";
                    case FieldDescriptorProto.Type.TypeString:
                        return "string";
                    case FieldDescriptorProto.Type.TypeSint32:
                        dataFormat = nameof(DataFormat.ZigZag);
                        return "int";
                    case FieldDescriptorProto.Type.TypeInt32:
                        return "int";
                    case FieldDescriptorProto.Type.TypeSfixed32:
                        dataFormat = nameof(DataFormat.FixedSize);
                        return "int";
                    case FieldDescriptorProto.Type.TypeSint64:
                        dataFormat = nameof(DataFormat.ZigZag);
                        return "long";
                    case FieldDescriptorProto.Type.TypeInt64:
                        return "long";
                    case FieldDescriptorProto.Type.TypeSfixed64:
                        dataFormat = nameof(DataFormat.FixedSize);
                        return "long";
                    case FieldDescriptorProto.Type.TypeFixed32:
                        dataFormat = nameof(DataFormat.FixedSize);
                        return "uint";
                    case FieldDescriptorProto.Type.TypeUint32:
                        return "uint";
                    case FieldDescriptorProto.Type.TypeFixed64:
                        dataFormat = nameof(DataFormat.FixedSize);
                        return "ulong";
                    case FieldDescriptorProto.Type.TypeUint64:
                        return "ulong";
                    case FieldDescriptorProto.Type.TypeBytes:
                        return "byte[]";
                    case FieldDescriptorProto.Type.TypeEnum:
                        var enumType = Find<EnumDescriptorProto>(field.TypeName);
                        return Normalizer.GetName(enumType);
                    case FieldDescriptorProto.Type.TypeGroup:
                    case FieldDescriptorProto.Type.TypeMessage:
                        var msgType = Find<DescriptorProto>(field.TypeName);
                        if(field.type == FieldDescriptorProto.Type.TypeGroup)
                        {
                            dataFormat = nameof(DataFormat.Group);
                        }
                        return Normalizer.GetName(msgType);
                    default:
                        if (field.type == 0)
                        {
                            throw new ParserException(field.TypeToken, $"unknown type: {field.TypeName}", true);
                        }
                        else
                        {
                            throw new ParserException(field.TypeToken, $"unknown type: {field.type} ({field.TypeName})", true);
                        }
                }
            }
            public GeneratorContext Indent()
            {
                _indent++;
                return this;
            }
            public GeneratorContext Outdent()
            {
                _indent--;
                return this;
            }
            private int _indent;
            public GeneratorContext(FileDescriptorProto schema, NameNormalizer normalizer, TextWriter output)
            {
                this.fileDescriptor = schema;
                Normalizer = normalizer;
                Output = output;
            }
            public TextWriter Write(string value)
            {
                var indent = _indent;
                var target = Output;
                while (indent-- > 0)
                {
                    target.Write(Tab);
                }
                target.Write(value);
                return target;
            }
            public string Tab { get; set; } = "    ";
            public GeneratorContext WriteLine()
            {
                Output.WriteLine();
                return this;
            }
            public GeneratorContext WriteLine(string line)
            {
                var indent = _indent;
                var target = Output;
                while (indent-- > 0)
                {
                    target.Write(Tab);
                }
                target.WriteLine(line);
                return this;
            }
            public TextWriter Output { get; }
            public NameNormalizer Normalizer { get; }

            private Dictionary<string, object> _knownTypes = new Dictionary<string, object>();
            private readonly FileDescriptorProto fileDescriptor;

            internal void BuildTypeIndex()
            {
                void AddMessage(DescriptorProto message)
                {
                    _knownTypes.Add(message.FullyQualifiedName, message);
                    foreach (var @enum in message.EnumTypes)
                    {
                        _knownTypes.Add(@enum.FullyQualifiedName, @enum);
                    }
                    foreach (var msg in message.NestedTypes)
                    {
                        AddMessage(msg);
                    }
                }
                {
                    _knownTypes.Clear();
                    foreach (var @enum in fileDescriptor.EnumTypes)
                    {
                        _knownTypes.Add(@enum.FullyQualifiedName, @enum);
                    }
                    foreach (var msg in fileDescriptor.MessageTypes)
                    {
                        AddMessage(msg);
                    }
                }
            }
            public T Find<T>(string typeName) where T : class
            {
                if(!_knownTypes.TryGetValue(typeName, out var obj) || obj == null)
                {
                    throw new InvalidOperationException($"Type not found: {typeName}");
                }
                if (obj is T) return (T)obj;

                throw new InvalidOperationException($"Type of {typeName} is not suitable; expected {typeof(T).Name}, got {obj.GetType().Name}");
            }
        }

        public static void GenerateCSharp(TextWriter target, FileDescriptorProto schema, NameNormalizer normalizer = null, IList<Error> errors = null)
        {
            var ctx = new GeneratorContext(schema, normalizer ?? NameNormalizer.Default, target);
            ctx.BuildTypeIndex();

            ctx.WriteLine("// This file is generated by a tool; you should avoid making direct changes.")
                .WriteLine("// Consider using 'partial classes' to extend these types")
                .WriteLine($"// Input: {Path.GetFileName(schema.Name)}").WriteLine();
            
            var @namespace = schema.Options?.CsharpNamespace ?? schema.Package;

            if(errors != null)
            {
                bool isFirst = true;
                foreach(var error in errors.Where(x => x.IsError))
                {
                    if(isFirst)
                    {
                        ctx.WriteLine("// errors in " + schema.Name);
                        isFirst = false;
                    }
                    ctx.WriteLine("#error " + error.ToString(false));
                }
                if (!isFirst) ctx.WriteLine();
            }
            ctx.WriteLine("#pragma warning disable CS1591").WriteLine();
            if (!string.IsNullOrWhiteSpace(@namespace))
            {
                ctx.WriteLine($"namespace {@namespace}");
                ctx.WriteLine("{").Indent().WriteLine();
            }
            foreach (var @enum in schema.EnumTypes)
            {
                Write(ctx, @enum);
            }
            foreach (var msgType in schema.MessageTypes)
            {
                Write(ctx, msgType);
            }
            if (!string.IsNullOrWhiteSpace(@namespace))
            {
                ctx.Outdent().WriteLine("}").WriteLine();
            }
            ctx.WriteLine("#pragma warning restore CS1591");
        }

        private static void Write(GeneratorContext context, DescriptorProto message)
        {
            var name = context.Normalizer.GetName(message);
            context.WriteLine($@"[global::ProtoBuf.ProtoContract(Name = @""{message.Name}"")]");
            WriteOptions(context, message.Options);
            context.WriteLine($"public partial class {Escape(name)}");
            context.WriteLine("{").Indent();
            foreach (var obj in message.EnumTypes)
            {
                Write(context, obj);
            }
            foreach (var obj in message.NestedTypes)
            {
                Write(context, obj);
            }
            foreach (var obj in message.Fields)
            {
                Write(context, obj);
            }
            context.Outdent().WriteLine("}").WriteLine();
        }
        private static bool UseArray(FieldDescriptorProto field)
        {
            switch (field.type)
            {
                case FieldDescriptorProto.Type.TypeBool:
                case FieldDescriptorProto.Type.TypeDouble:
                case FieldDescriptorProto.Type.TypeFixed32:
                case FieldDescriptorProto.Type.TypeFixed64:
                case FieldDescriptorProto.Type.TypeFloat:
                case FieldDescriptorProto.Type.TypeInt32:
                case FieldDescriptorProto.Type.TypeInt64:
                case FieldDescriptorProto.Type.TypeSfixed32:
                case FieldDescriptorProto.Type.TypeSfixed64:
                case FieldDescriptorProto.Type.TypeSint32:
                case FieldDescriptorProto.Type.TypeSint64:
                case FieldDescriptorProto.Type.TypeUint32:
                case FieldDescriptorProto.Type.TypeUint64:
                    return true;
                default:
                    return false;
            }
        }

        private static void WriteOptions<T>(GeneratorContext context, T obj) where T : class, ISchemaOptions
        {
            if (obj == null) return;
            if(obj.Deprecated)
            {
                context.WriteLine($"[global::System.Obsolete]");
            }
        }
        const string FieldPrefix = "__pbn__";
        private static void Write(GeneratorContext context, FieldDescriptorProto field)
        {
            var name = context.Normalizer.GetName(field);
            var tw = context.Write($@"[global::ProtoBuf.ProtoMember({field.Number}, Name = @""{field.Name}""");
            bool isOptional = field.label == FieldDescriptorProto.Label.LabelOptional;
            bool isRepeated = field.label == FieldDescriptorProto.Label.LabelRepeated;

            bool explicitValues = isOptional && context.Syntax == FileDescriptorProto.SyntaxProto2
                && field.type != FieldDescriptorProto.Type.TypeMessage
                && field.type != FieldDescriptorProto.Type.TypeGroup;

            string defaultValue = null;
            if (isOptional)
            {
                defaultValue = field.DefaultValue;

                if (field.type == FieldDescriptorProto.Type.TypeString)
                {
                    defaultValue = string.IsNullOrEmpty(defaultValue) ? "\"\""
                        : ("@\"" + (defaultValue ?? "").Replace("\"", "\"\"") + "\"");
                }
                else if (!string.IsNullOrWhiteSpace(defaultValue) && field.type == FieldDescriptorProto.Type.TypeEnum)
                {
                    var enumType = context.Find<EnumDescriptorProto>(field.TypeName);

                    var found = enumType.Values.FirstOrDefault(x => x.Name == defaultValue);
                    if (found != null) defaultValue = context.Normalizer.GetName(found);
                    defaultValue = context.Normalizer.GetName(enumType) + "." + defaultValue;
                }
            }
            var typeName = context.GetTypeName(field, out var dataFormat);
            if (!string.IsNullOrWhiteSpace(dataFormat))
            {
                tw.Write($", DataFormat = DataFormat.{dataFormat}");
            }
            if (field.Options?.Packed ?? false)
            {
                tw.Write($", IsPacked = true");
            }
            if (field.label == FieldDescriptorProto.Label.LabelRequired)
            {
                tw.Write($", IsRequired = true");
            }
            tw.WriteLine(")]");
            if (!isRepeated && !string.IsNullOrWhiteSpace(defaultValue))
            {
                context.WriteLine($"[global::System.ComponentModel.DefaultValue({defaultValue})]");
            }
            WriteOptions(context, field.Options);
            if (isRepeated)
            {
                if (UseArray(field))
                {
                    context.WriteLine($"public {typeName}[] {Escape(name)} {{ get; set; }}");
                }
                else
                {
                    context.WriteLine($"public global::System.Collections.Generic.List<{typeName}> {Escape(name)} {{ get; }} = new global::System.Collections.Generic.List<{typeName}>();");
                }
            }
            else if(explicitValues)
            {
                string fieldName = FieldPrefix + name, fieldType;
                bool isRef = false;
                switch(field.type)
                {
                    case FieldDescriptorProto.Type.TypeString:
                    case FieldDescriptorProto.Type.TypeBytes:
                        fieldType = typeName;
                        isRef = true;
                        break;
                    default:
                        fieldType = typeName + "?";
                        break;
                }
                context.WriteLine($"public {typeName} {Escape(name)}").WriteLine("{").Indent();
                tw = context.Write($"get {{ return {fieldName}");
                if (!string.IsNullOrWhiteSpace(defaultValue))
                {
                    tw.Write(" ?? ");
                    tw.Write(defaultValue);
                }
                else if (!isRef)
                {
                    tw.Write(".GetValueOrDefault()");
                }
                tw.WriteLine("; }");
                context.WriteLine($"set {{ {fieldName} = value; }}")
                    .Outdent().WriteLine("}")
                    .WriteLine($"public bool ShouldSerialize{name}() => {fieldName} != null;")
                    .WriteLine($"public void Reset{name}() => {fieldName} = null;")
                    .WriteLine($"private {fieldType} {fieldName};");
            }
            else
            {
                tw = context.Write($"public {typeName} {Escape(name)} {{ get; set; }}");
                if (!string.IsNullOrWhiteSpace(defaultValue)) tw.Write($" = {defaultValue};");
                tw.WriteLine();
            }
            context.WriteLine();
        }


    }
}
