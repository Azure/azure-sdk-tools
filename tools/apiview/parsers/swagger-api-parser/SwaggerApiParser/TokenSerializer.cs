using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using APIView;

namespace SwaggerApiParser
{
    public class IteratorPath
    {
        private LinkedList<string> paths;

        public IteratorPath()
        {
            this.paths = new LinkedList<string>();
            this.Add("#");
        }

        public IteratorPath(IteratorPath parent)
        {
            this.paths = new LinkedList<string>();
            foreach (var path in parent.paths)
            {
                this.paths.AddLast(path);
            }
        }

        public void Add(string node)
        {
            this.paths.AddLast(node);
        }

        public void Pop()
        {
            this.paths.RemoveLast();
        }

        public string CurrentPath()
        {
            return Utils.BuildDefinitionId(this.paths);
        }
    }

    public class SerializeContext
    {
        public readonly int intent = 0;
        public readonly IteratorPath IteratorPath;

        public SerializeContext()
        {
            this.IteratorPath = new IteratorPath();
        }

        public SerializeContext(int intent, IteratorPath iteratorPath)
        {
            this.intent = intent;
            this.IteratorPath = new IteratorPath(iteratorPath);
        }
        
        
    }

    public static class TokenSerializer
    {
        private const String IntentText = "  ";

        public static CodeFileToken[] TokenSerialize(object obj, int intent = 0, String[] serializePropertyName = null)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            Type t = obj.GetType();
            PropertyInfo[] props = t.GetProperties();
            foreach (var prop in props)
            {
                object value = prop.GetValue(obj);
                if (value == null || (serializePropertyName != null && (serializePropertyName.All(s => prop.Name != s))))
                {
                    continue;
                }


                Type propType = prop.PropertyType;
                ret.Add(Intent(intent));
                ret.Add(new CodeFileToken(prop.Name, CodeFileTokenKind.Literal));
                ret.Add(new CodeFileToken(":", CodeFileTokenKind.Punctuation));
                if (propType.IsPrimitive || propType == typeof(Decimal) || propType == typeof(String))
                {
                    ret.Add(new CodeFileToken(value.ToString(), CodeFileTokenKind.Literal));
                    ret.Add(NewLine());
                }
                else
                {
                    ret.Add(NewLine());
                    var child = TokenSerializer.TokenSerialize(value, intent + 1);
                    ret.AddRange(child);
                }
            }

            return ret.ToArray();
        }

        public static CodeFileToken Intent(int intent)
        {
            return new CodeFileToken(String.Concat(Enumerable.Repeat(IntentText, intent)), CodeFileTokenKind.Whitespace);
        }

        public static CodeFileToken NewLine()
        {
            return new CodeFileToken("", CodeFileTokenKind.Newline);
        }

        public static CodeFileToken NavigableToken(String value, CodeFileTokenKind kind, String definitionId)
        {
            var ret = new CodeFileToken(value, kind) {DefinitionId = definitionId};
            return ret;
        }
    }

    public class Visitor
    {
        private SwaggerTokenSerializer.IndentWriter _writer = new();
        private IteratorPath iteratorPath = new();


        public static CodeFileToken[] GenerateCodeFileTokens(JsonDocument document, Boolean isCurObjCollapsible = false)
        {
            Visitor visitor = new();

            visitor.Visit(document.RootElement);
            return visitor._writer.ToTokens();
        }

        /// <summary>
        /// Generate the listing for a JSON value.
        /// </summary>
        /// <param name="element">The JSON value.</param>
        /// <param name="scopeStart"></param>
        /// <param name="scopeEnd"></param>
        private void Visit(JsonElement element, string scopeStart = "{ ", string scopeEnd = " }")
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    VisitObject(element, scopeStart, scopeEnd);
                    break;
                case JsonValueKind.Array:
                    VisitArray(element);
                    break;
                case JsonValueKind.False:
                case JsonValueKind.Null:
                case JsonValueKind.Number:
                case JsonValueKind.String:
                case JsonValueKind.True:
                case JsonValueKind.Undefined:
                    VisitLiteral(element);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Generate the listing for an object.
        /// </summary>
        /// <param name="obj">The JSON object.</param>
        /// <param name="scopeStart"></param>
        /// <param name="scopeEnd"></param>
        private void VisitObject(JsonElement obj, string scopeStart = "{", string scopeEnd = "}")
        {
            bool multiLine = !FitsOnOneLine(obj);

            using (_writer.Scope(scopeStart, scopeEnd, multiLine))
            {
                // Optionally sort the values
                IEnumerable<JsonProperty> values = obj.EnumerateObject();
                SwaggerTokenSerializer.Fenceposter fencepost = new();

                // Generate the listing for each property
                foreach (JsonProperty property in values)
                {
                    // Add the property to the current path
                    iteratorPath.Add(property.Name);

                    var isCollapsible = IsCurObjCollapsible(property.Name);
                    // Write the property name
                    _writer.Write(CodeFileTokenKind.Punctuation, "\"");
                    var propertyType = isCollapsible ? CodeFileTokenKind.TypeName : CodeFileTokenKind.MemberName;
                    _writer.Write(propertyType, property.Name);


                    // Create an ID for this property


                    string id = this.iteratorPath.CurrentPath();
                    _writer.AnnotateDefinition(id);
                    if (isCollapsible)
                    {
                        _writer.Write(CodeFileTokenKind.FoldableParentToken, id);
                    }

                    // Visit the value
                    if (isCollapsible)
                    {
                        _writer.Write(CodeFileTokenKind.Punctuation, "\": ");
                        this._writer.WriteLine();
                        this._writer.Write(CodeFileTokenKind.FoldableContentStart, null);
                        Visit(property.Value);
                        if (property.Name != values.Last().Name)
                        {
                            _writer.Write(CodeFileTokenKind.Punctuation, ", ");
                            if (multiLine) { _writer.WriteLine(); }
                        }

                        this._writer.Write(CodeFileTokenKind.FoldableContentEnd, null);
                    }
                    else
                    {
                        _writer.Write(CodeFileTokenKind.Punctuation, "\": ");
                        Visit(property.Value);
                        if (property.Name != values.Last().Name)
                        {
                            _writer.Write(CodeFileTokenKind.Punctuation, ", ");
                            if (multiLine) { _writer.WriteLine(); }
                        }
                    }

                    // Remove the property from the current path
                    this.iteratorPath.Pop();
                }
            }
        }

        private static bool IsCurObjCollapsible(string propertyName)
        {
            return false;
            // return propertyName.Equals("General");
        }

        /// <summary>
        /// Generate the listing for an array.
        /// </summary>
        /// <param name="array">The array.</param>
        /// <param name="scopeStart"></param>
        /// <param name="scopeEnd"></param>
        private void VisitArray(JsonElement array, string scopeStart = "[ ", string scopeEnd = " ]")
        {
            bool multiLine = !FitsOnOneLine(array);
            using (_writer.Scope(scopeStart, scopeEnd, multiLine))
            {
                int index = 0;
                SwaggerTokenSerializer.Fenceposter fencepost = new();

                foreach (JsonElement child in array.EnumerateArray())
                {
                    if (fencepost.RequiresSeparator)
                    {
                        _writer.Write(CodeFileTokenKind.Punctuation, ", ");
                        if (multiLine) { _writer.WriteLine(); }
                    }

                    this.iteratorPath.Add(index.ToString());
                    Visit(child);
                    this.iteratorPath.Pop();
                    index++;
                }
            }
        }

        /// <summary>
        /// Generate the listing for a literal value.
        /// </summary>
        /// <param name="value">The literal value.</param>
        private void VisitLiteral(JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Null:
                    _writer.Write(CodeFileTokenKind.Keyword, "null");
                    break;
                case JsonValueKind.Undefined:
                    _writer.Write(CodeFileTokenKind.Keyword, "undefined");
                    break;
                case JsonValueKind.True:
                    _writer.Write(CodeFileTokenKind.Keyword, "true");
                    break;
                case JsonValueKind.False:
                    _writer.Write(CodeFileTokenKind.Keyword, "false");
                    break;
                case JsonValueKind.Number:
                    _writer.Write(CodeFileTokenKind.Literal, value.GetDouble().ToString());
                    break;
                case JsonValueKind.String:
                    _writer.Write(CodeFileTokenKind.Punctuation, "\"");
                    _writer.Write(CodeFileTokenKind.StringLiteral, value.GetString());
                    this.iteratorPath.Add(value.GetString());
                    this._writer.AnnotateDefinition(this.iteratorPath.CurrentPath());
                    this.iteratorPath.Pop();
                    _writer.Write(CodeFileTokenKind.Punctuation, "\"");
                    break;
                default:
                    throw new InvalidOperationException($"Expected a literal JSON element, not {value.ValueKind}.");
            }
        }

        /// <summary>
        /// Crude heuristic to determine whether a JsonElement can be
        /// rendered on a single line.
        /// </summary>
        /// <param name="element">The JSON to render.</param>
        /// <returns>Whether it fits on a single line.</returns>
        private bool FitsOnOneLine(JsonElement element)
        {
            const int maxObjectProperties = 2;
            const int maxArrayElements = 3;
            const int maxStringLength = 50;

            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    int properties = 0;
                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        if (property.Value.ValueKind == JsonValueKind.Array ||
                            property.Value.ValueKind == JsonValueKind.Object ||
                            !FitsOnOneLine(property.Value) ||
                            properties++ > maxObjectProperties)
                        {
                            return false;
                        }
                    }

                    return true;
                case JsonValueKind.Array:
                    int values = 0;
                    foreach (JsonElement value in element.EnumerateArray())
                    {
                        if (value.ValueKind == JsonValueKind.Array ||
                            value.ValueKind == JsonValueKind.Object ||
                            !FitsOnOneLine(value) ||
                            values++ > maxArrayElements)
                        {
                            return false;
                        }
                    }

                    return true;
                case JsonValueKind.String:
                    return element.GetString().Length <= maxStringLength;
                case JsonValueKind.False:
                case JsonValueKind.Null:
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.Undefined:
                default:
                    return true;
            }
        }

        /// <summary>
        /// Get a string in a JSON document. 
        /// </summary>
        /// <param name="doc">The JSON document.</param>
        /// <param name="path">Path to the string.</param>
        /// <returns>The desired string, or null.</returns>
        private static string GetString(JsonDocument doc, params string[] path) =>
            GetString(doc.RootElement, path);

        /// <summary>
        /// Get a string in a JSON document. 
        /// </summary>
        /// <param name="element">The element to start at.</param>
        /// <param name="path">Path to the string.</param>
        /// <returns>The desired string, or null.</returns>
        private static string GetString(JsonElement element, params string[] path)
        {
            foreach (string part in path)
            {
                if (element.ValueKind != JsonValueKind.Object ||
                    !element.TryGetProperty(part, out element))
                {
                    return null;
                }
            }

            return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
        }

        /// <summary>
        /// Comparer to sort the operations within a path.  It puts
        /// OperationIds before things like parameters.
        /// </summary>
        private class OperationComparer : IComparer<string>
        {
            /// <summary>
            /// Compare two path entry names.
            /// </summary>
            /// <param name="x">The first path entry name.</param>
            /// <param name="y">The second path entry name.</param>
            /// <returns>-1 if the first is smaller, 0 if equal, or 1 if the first is larger.</returns>
            public int Compare([AllowNull] string x, [AllowNull] string y) =>
                (x?.Contains('_'), y?.Contains('_')) switch
                {
                    (null, null) => 0,
                    (null, _) => 1,
                    (_, null) => -1,
                    (true, false) => -1,
                    (false, true) => 1,
                    _ => string.Compare(x, y)
                };
        }
    }
}
