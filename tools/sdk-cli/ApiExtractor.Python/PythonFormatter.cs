// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

namespace ApiExtractor.Python;

/// <summary>
/// Formats an ApiIndex as human-readable Python stub syntax.
/// </summary>
public static class PythonFormatter
{
    public static string Format(ApiIndex index)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# {index.Package} - Public API Surface");
        sb.AppendLine($"# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        foreach (var module in index.Modules ?? [])
        {
            sb.AppendLine($"# Module: {module.Name}");
            sb.AppendLine();

            // Module-level functions
            foreach (var func in module.Functions ?? [])
            {
                FormatFunction(sb, func, "");
            }

            // Classes
            foreach (var cls in module.Classes ?? [])
            {
                FormatClass(sb, cls);
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void FormatClass(StringBuilder sb, ClassInfo cls)
    {
        // Docstring
        if (!string.IsNullOrEmpty(cls.Doc))
            sb.AppendLine($"    \"\"\"{cls.Doc}\"\"\"");

        // Class declaration
        var baseClass = !string.IsNullOrEmpty(cls.Base) ? $"({cls.Base})" : "";
        sb.AppendLine($"class {cls.Name}{baseClass}:");

        var hasMembers = false;

        // Properties
        foreach (var prop in cls.Properties ?? [])
        {
            if (!string.IsNullOrEmpty(prop.Doc))
                sb.AppendLine($"        \"\"\"{prop.Doc}\"\"\"");

            var typeHint = !string.IsNullOrEmpty(prop.Type) ? $": {prop.Type}" : "";
            sb.AppendLine($"    {prop.Name}{typeHint}");
            hasMembers = true;
        }

        // Methods
        foreach (var method in cls.Methods ?? [])
        {
            FormatMethod(sb, method, "    ");
            hasMembers = true;
        }

        if (!hasMembers)
            sb.AppendLine("    ...");

        sb.AppendLine();
    }

    private static void FormatMethod(StringBuilder sb, MethodInfo method, string indent)
    {
        if (!string.IsNullOrEmpty(method.Doc))
            sb.AppendLine($"{indent}    \"\"\"{method.Doc}\"\"\"");

        var decorators = new List<string>();
        if (method.IsClassMethod == true) decorators.Add("@classmethod");
        if (method.IsStaticMethod == true) decorators.Add("@staticmethod");

        foreach (var dec in decorators)
            sb.AppendLine($"{indent}{dec}");

        var asyncPrefix = method.IsAsync == true ? "async " : "";
        sb.AppendLine($"{indent}{asyncPrefix}def {method.Name}({method.Signature}): ...");
    }

    private static void FormatFunction(StringBuilder sb, FunctionInfo func, string indent)
    {
        if (!string.IsNullOrEmpty(func.Doc))
            sb.AppendLine($"{indent}\"\"\"{func.Doc}\"\"\"");

        var asyncPrefix = func.IsAsync == true ? "async " : "";
        sb.AppendLine($"{indent}{asyncPrefix}def {func.Name}({func.Signature}): ...");
        sb.AppendLine();
    }
}
