// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

namespace ApiExtractor.DotNet;

/// <summary>
/// Formats an ApiIndex as human-readable C# syntax.
/// </summary>
public static class CSharpFormatter
{
    public static string Format(ApiIndex index)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"// {index.Package} - Public API Surface");
        sb.AppendLine($"// Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        
        foreach (var ns in index.Namespaces ?? [])
        {
            sb.AppendLine($"namespace {ns.Name}");
            sb.AppendLine("{");
            
            foreach (var type in ns.Types ?? [])
                FormatType(sb, type, "    ");
            
            sb.AppendLine("}");
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    private static void FormatType(StringBuilder sb, TypeInfo type, string indent)
    {
        // XML doc
        if (!string.IsNullOrEmpty(type.Doc))
        {
            sb.AppendLine($"{indent}/// <summary>{EscapeXml(type.Doc)}</summary>");
        }
        
        // Type declaration
        var inheritance = BuildInheritance(type);
        sb.Append($"{indent}public {type.Kind} {type.Name}");
        if (!string.IsNullOrEmpty(inheritance))
            sb.Append($" : {inheritance}");
        
        // Enum values
        if (type.Kind == "enum" && type.Values?.Count > 0)
        {
            sb.AppendLine($" {{ {string.Join(", ", type.Values)} }}");
            sb.AppendLine();
            return;
        }
        
        sb.AppendLine();
        sb.AppendLine($"{indent}{{");
        
        // Members grouped by kind
        var members = type.Members ?? [];
        
        // Constants first
        foreach (var m in members.Where(m => m.Kind == "const"))
            FormatMember(sb, m, indent + "    ");
        
        // Static properties
        foreach (var m in members.Where(m => m.Kind == "property" && m.IsStatic == true))
            FormatMember(sb, m, indent + "    ");
        
        // Constructors
        foreach (var m in members.Where(m => m.Kind == "ctor"))
            FormatMember(sb, m, indent + "    ");
        
        // Instance properties
        foreach (var m in members.Where(m => m.Kind == "property" && m.IsStatic != true))
            FormatMember(sb, m, indent + "    ");
        
        // Indexers
        foreach (var m in members.Where(m => m.Kind == "indexer"))
            FormatMember(sb, m, indent + "    ");
        
        // Events
        foreach (var m in members.Where(m => m.Kind == "event"))
            FormatMember(sb, m, indent + "    ");
        
        // Methods
        foreach (var m in members.Where(m => m.Kind == "method"))
            FormatMember(sb, m, indent + "    ");
        
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }
    
    private static void FormatMember(StringBuilder sb, MemberInfo member, string indent)
    {
        // XML doc
        if (!string.IsNullOrEmpty(member.Doc))
            sb.AppendLine($"{indent}/// <summary>{EscapeXml(member.Doc)}</summary>");
        
        var modifiers = new List<string> { "public" };
        if (member.IsStatic == true) modifiers.Add("static");
        if (member.IsAsync == true && member.Kind == "method") modifiers.Add("async");
        
        var mods = string.Join(" ", modifiers);
        
        // Properties/indexers already have { get; } in signature - don't add semicolon
        var sig = member.Signature;
        var needsSemicolon = !sig.EndsWith("}");
        var suffix = needsSemicolon ? ";" : "";
        
        switch (member.Kind)
        {
            case "ctor":
                sb.AppendLine($"{indent}public {member.Name}{sig};");
                break;
            case "property":
            case "indexer":
                sb.AppendLine($"{indent}{mods} {sig}{suffix}");
                break;
            case "event":
                sb.AppendLine($"{indent}{mods} {sig};");
                break;
            case "method":
                sb.AppendLine($"{indent}{mods} {sig};");
                break;
            case "const":
                sb.AppendLine($"{indent}public {sig};");
                break;
        }
    }
    
    private static string BuildInheritance(TypeInfo type)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(type.Base))
            parts.Add(type.Base);
        if (type.Interfaces?.Count > 0)
            parts.AddRange(type.Interfaces);
        return string.Join(", ", parts);
    }
    
    private static string EscapeXml(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
