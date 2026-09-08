using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Azure.Sdk.Tools.CodeownersMigration.Conversion
{
    /// <summary>
    /// Minimal YAML emitter. A serializer is deliberately not used: the generated owners files are read and
    /// edited by service teams, so key order, flow-style owner lists and explanatory comments all matter, and
    /// none of those survive a round trip through a general purpose object serializer.
    /// </summary>
    public class YamlWriter
    {
        private readonly StringBuilder _builder = new StringBuilder();
        private int _indent;

        public YamlWriter Indent()
        {
            _indent++;
            return this;
        }

        public YamlWriter Outdent()
        {
            if (_indent > 0)
            {
                _indent--;
            }
            return this;
        }

        public YamlWriter Comment(string text)
        {
            foreach (string line in SplitLines(text))
            {
                _builder.Append(Prefix()).Append(line.Length == 0 ? "#" : "# " + line).Append('\n');
            }
            return this;
        }

        public YamlWriter Blank()
        {
            _builder.Append('\n');
            return this;
        }

        public YamlWriter Key(string key)
        {
            _builder.Append(Prefix()).Append(Escape(key)).Append(":").Append('\n');
            return this;
        }

        public YamlWriter Scalar(string key, string value)
        {
            _builder.Append(Prefix()).Append(Escape(key)).Append(": ").Append(Escape(value)).Append('\n');
            return this;
        }

        public YamlWriter Scalar(string key, bool value)
        {
            _builder.Append(Prefix()).Append(Escape(key)).Append(": ").Append(value ? "true" : "false").Append('\n');
            return this;
        }

        public YamlWriter Scalar(string key, int value)
        {
            _builder.Append(Prefix()).Append(Escape(key)).Append(": ").Append(value).Append('\n');
            return this;
        }

        /// <summary>Emits a flow sequence, e.g. <c>owners: [a, b]</c>. Omitted entirely when empty.</summary>
        public YamlWriter FlowSequence(string key, IEnumerable<string> values)
        {
            List<string> list = (values ?? Enumerable.Empty<string>()).ToList();
            if (list.Count == 0)
            {
                return this;
            }
            _builder.Append(Prefix())
                    .Append(Escape(key))
                    .Append(": [")
                    .Append(string.Join(", ", list.Select(Escape)))
                    .Append("]\n");
            return this;
        }

        /// <summary>
        /// Starts a block sequence item. Subsequent calls at the same indent continue the item's mapping, so
        /// the caller emits <c>- key: value</c> followed by aligned sibling keys.
        /// </summary>
        public YamlWriter ItemScalar(string key, string value)
        {
            _builder.Append(ItemPrefix()).Append(Escape(key)).Append(": ").Append(Escape(value)).Append('\n');
            return this;
        }

        /// <summary>Emits a bare block sequence item, e.g. <c>- "sdk/*/owners.yaml"</c>.</summary>
        public YamlWriter ItemScalarBare(string value)
        {
            _builder.Append(ItemPrefix()).Append(Escape(value)).Append('\n');
            return this;
        }

        public YamlWriter ItemFlowSequence(string key, IEnumerable<string> values)
        {
            List<string> list = (values ?? Enumerable.Empty<string>()).ToList();
            _builder.Append(ItemPrefix())
                    .Append(Escape(key))
                    .Append(": [")
                    .Append(string.Join(", ", list.Select(Escape)))
                    .Append("]\n");
            return this;
        }

        public override string ToString() => _builder.ToString();

        private string Prefix() => new string(' ', _indent * 2);

        private string ItemPrefix()
        {
            return new string(' ', _indent * 2) + "- ";
        }

        private static IEnumerable<string> SplitLines(string text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        }

        /// <summary>
        /// Quotes a scalar when leaving it bare would change its meaning or break parsing. Conservative by
        /// design: a false positive only costs a pair of quotes, a false negative corrupts the file.
        /// </summary>
        public static string Escape(string value)
        {
            if (value == null)
            {
                return "\"\"";
            }

            bool needsQuotes =
                value.Length == 0 ||
                value != value.Trim() ||
                // Path expressions are globs. Quoting anything containing a wildcard keeps '*' from reading as
                // a YAML alias and makes the expression unambiguous to a human reviewer.
                value.Contains("*") ||
                value.Contains(": ") ||
                value.Contains(" #") ||
                value.Contains("\"") ||
                value.Contains("'") ||
                value.Contains("\\") ||
                value.Contains("\n") ||
                value.Contains("[") ||
                value.Contains("]") ||
                value.Contains("{") ||
                value.Contains("}") ||
                value.Contains(",") ||
                value.EndsWith(":") ||
                "-?:,[]{}#&*!|>'\"%@`".IndexOf(value[0]) >= 0 ||
                IsReservedWord(value);

            if (!needsQuotes)
            {
                return value;
            }

            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static bool IsReservedWord(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "true":
                case "false":
                case "yes":
                case "no":
                case "on":
                case "off":
                case "null":
                case "~":
                    return true;
                default:
                    return double.TryParse(value, out _);
            }
        }
    }
}
