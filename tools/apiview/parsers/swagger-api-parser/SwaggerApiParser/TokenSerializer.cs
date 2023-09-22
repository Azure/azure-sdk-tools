using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using SwaggerApiParser.SwaggerApiView;

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

        public string rootPath()
        {
            LinkedList<string> root = new LinkedList<string>();
            root.AddLast("#");
            root.AddLast(this.paths.ElementAt(1));
            return Utils.BuildDefinitionId(root);
        }

        public void Add(string node)
        {
            this.paths.AddLast(node);
        }

        public void AddRange(IEnumerable<string> nodes)
        {
            foreach (var node in nodes)
            {
                this.Add(node);
            }
        }

        public void PopMulti(int number)
        {
            for (int i = 0; i < number; i++)
            {
                this.Pop();
            }
        }

        public void Pop()
        {
            this.paths.RemoveLast();
        }

        public string CurrentPath()
        {
            return Utils.BuildDefinitionId(this.paths);
        }

        public string CurrentNextPath(string nextPath)
        {
            this.Add(nextPath);
            var ret = this.CurrentPath();
            this.Pop();
            return ret;
        }
    }

    public class SerializeContext
    {
        public int indent = 0;
        public readonly IteratorPath IteratorPath;
        public List<string> definitionsNames { get; set; }

        public SerializeContext()
        {
            this.IteratorPath = new IteratorPath();
        }

        public SerializeContext(int indent, IteratorPath iteratorPath)
        {
            this.indent = indent;
            this.IteratorPath = new IteratorPath(iteratorPath);
        }

        public SerializeContext(int indent, IteratorPath iteratorPath, List<string> definitionNames)
        {
            this.indent = indent;
            this.IteratorPath = new IteratorPath(iteratorPath);
            this.definitionsNames = definitionNames;
        }
    }

    public static class TokenSerializer
    {
        private const String IntentText = "  ";

        public static CodeFileToken[] TokenSerializeAsJson(JsonElement jsonElement, bool isFoldable = false)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            Visitor visitor = new Visitor();
            visitor.Visit(jsonElement);
            if (isFoldable)
            {
                ret.Add(TokenSerializer.FoldableContentStart());
            }

            ret.AddRange(visitor.Writer.ToTokens());
            ret.Add(TokenSerializer.NewLine());
            if (isFoldable)
            {
                ret.Add(TokenSerializer.FoldableContentEnd());
            }

            return ret.ToArray();
        }

        public static CodeFileToken[] TokenSerializeJsonToCodeLines(JsonElement jsonElement)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            Visitor visitor = new Visitor();
            visitor.Visit(jsonElement, excludeJsonPunctuations: true);
            ret.AddRange(visitor.Writer.ToTokens());
            ret.Add(TokenSerializer.NewLine());
            return ret.ToArray();
        }

        /*
         * TokenSerialize obj into CodeFileTokens.
         * Each line begin with indent
         * One line format: <indent> <token 1> <token 2> ... <newline>
         */
        public static CodeFileToken[] TokenSerialize(object obj, SerializeContext context, String[] serializePropertyName = null)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            Type t = obj.GetType();
            PropertyInfo[] props = t.GetProperties();

            if (t.IsPrimitive || t == typeof(Decimal) || t == typeof(String))
            {
                // ret.Add(Intent(intent));
                ret.Add(new CodeFileToken(obj.ToString(), CodeFileTokenKind.Literal));
                ret.Add(TokenSerializer.NewLine());
                return ret.ToArray();
            }

            if (t.Name == "JsonElement")
            {
                ret.AddRange(TokenSerializer.TokenSerializeAsJson((JsonElement)obj, true));
                return ret.ToArray();
            }

            foreach (var prop in props)
            {
                object value = prop.GetValue(obj);
                if (value == null || (serializePropertyName != null && (serializePropertyName.All(s => prop.Name != s))))
                {
                    continue;
                }

                Type propType = prop.PropertyType;
                // ret.Add(Intent(intent));
                ret.Add(new CodeFileToken(prop.Name, CodeFileTokenKind.Literal) { DefinitionId = context.IteratorPath.CurrentNextPath(prop.Name) });
                ret.Add(Colon());
                string navigationToId = null;
                var valueKind = CodeFileTokenKind.Literal;

                if (prop.Name == "@ref")
                {
                    navigationToId = context.IteratorPath.rootPath() + Utils.GetRefDefinitionIdPath(value.ToString());
                    valueKind = CodeFileTokenKind.MemberName;
                }

                if (propType.IsPrimitive || propType == typeof(Decimal) || propType == typeof(String))
                {
                    ret.Add(new CodeFileToken(value.ToString(), valueKind) { NavigateToId = navigationToId });
                    ret.Add(NewLine());
                }
                else if (propType.IsGenericType || propType.IsArray)
                {
                    ret.Add(NewLine());
                    if (prop.Name.Equals("patterenedObjects"))
                    {
                        Utils.SerializePatternedObjects((value as Dictionary<string, JsonElement>), ret);
                    }
                    else 
                    {
                        foreach (var item in (IEnumerable)value)
                        {
                            var child = TokenSerializer.TokenSerialize(item, new SerializeContext(indent: context.indent + 1, context.IteratorPath));
                            ret.AddRange(child);
                        }
                    }
                }
                else
                {
                    ret.Add(NewLine());
                    var child = TokenSerializer.TokenSerialize(value, new SerializeContext(indent: context.indent + 1, context.IteratorPath));
                    ret.AddRange(child);
                }
            }

            return ret.ToArray();
        }

        public static CodeFileToken[] TokenSerializeAsTableFormat(int rowCount, int columnCount, IEnumerable<String> columnNames, CodeFileToken[] rows, string tableDefinitionId)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();

            ret.Add(TokenSerializer.TableBegin(tableDefinitionId));
            ret.AddRange(TokenSerializer.TableSize(rowCount, columnCount));
            ret.AddRange(columnNames.Select(TokenSerializer.TableColumnName));
            ret.AddRange(rows);
            ret.Add(TokenSerializer.TableEnd());
            return ret.ToArray();
        }


        public static CodeFileToken Intent(int intent)
        {
            // var ret = new CodeFileToken(String.Concat(Enumerable.Repeat(IntentText, intent)), CodeFileTokenKind.Whitespace);
            var ret = new CodeFileToken(intent.ToString(), CodeFileTokenKind.Whitespace);
            return ret;
        }

        public static CodeFileToken[] TableCell(IEnumerable<CodeFileToken> tokens)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            ret.Add(new CodeFileToken(null, CodeFileTokenKind.TableCellBegin));
            ret.AddRange(tokens);
            ret.Add(new CodeFileToken(null, CodeFileTokenKind.TableCellEnd));
            return ret.ToArray();
        }

        public static CodeFileToken[] OneLineToken(int intent, IEnumerable<CodeFileToken> contentTokens)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            // ret.Add(TokenSerializer.Intent(intent));
            ret.AddRange(contentTokens);
            ret.Add(NewLine());
            return ret.ToArray();
        }

        public static CodeFileToken NewLine()
        {
            return new CodeFileToken("", CodeFileTokenKind.Newline);
        }

        public static CodeFileToken Colon()
        {
            return new CodeFileToken(": ", CodeFileTokenKind.Punctuation);
        }

        public static CodeFileToken NavigableToken(String value, CodeFileTokenKind kind, String definitionId)
        {
            var ret = new CodeFileToken(value, kind) { DefinitionId = definitionId };
            return ret;
        }

        public static CodeFileToken[] KeyValueTokens(String key, String value, bool newLine = true, string keyDefinitionId = null)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            ret.Add(TokenSerializer.NavigableToken(key, CodeFileTokenKind.Literal, keyDefinitionId));
            ret.Add(TokenSerializer.Colon());
            ret.Add(new CodeFileToken(value, CodeFileTokenKind.Literal));
            if (newLine)
            {
                ret.Add(TokenSerializer.NewLine());
            }

            return ret.ToArray();
        }

        public static CodeFileToken FoldableParentToken(String value)
        {
            var ret = new CodeFileToken(value, CodeFileTokenKind.FoldableSectionHeading);
            return ret;
        }

        public static CodeFileToken FoldableContentStart()
        {
            var ret = new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentStart);
            return ret;
        }

        public static CodeFileToken TableBegin(string definitionId)
        {
            var ret = new CodeFileToken(null, CodeFileTokenKind.TableBegin) { DefinitionId = definitionId };
            return ret;
        }

        public static CodeFileToken TableEnd()
        {
            var ret = new CodeFileToken(null, CodeFileTokenKind.TableEnd);
            return ret;
        }

        public static CodeFileToken[] TableSize(int row, int column)
        {
            var ret = new List<CodeFileToken>();
            ret.Add(new CodeFileToken(row.ToString(), CodeFileTokenKind.TableRowCount));
            ret.Add(new CodeFileToken(column.ToString(), CodeFileTokenKind.TableColumnCount));

            return ret.ToArray();
        }

        public static CodeFileToken TableColumnName(string columnName)
        {
            var ret = new CodeFileToken(columnName, CodeFileTokenKind.TableColumnName);
            return ret;
        }

        public static CodeFileToken FoldableContentEnd()
        {
            var ret = new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentEnd);
            return ret;
        }
    }

    public class Visitor
    {
        public SwaggerTokenSerializer.IndentWriter Writer = new();
        private IteratorPath iteratorPath = new();


        public static CodeFileToken[] GenerateCodeFileTokens(JsonDocument document, Boolean isCurObjCollapsible = false)
        {
            Visitor visitor = new();

            visitor.Visit(document.RootElement);
            return visitor.Writer.ToTokens();
        }

        /// <summary>
        /// Generate the listing for a JSON value.
        /// </summary>
        /// <param name="element">The JSON value.</param>
        /// <param name="scopeStart"></param>
        /// <param name="scopeEnd"></param>
        public void Visit(JsonElement element, string scopeStart = "{ ", string scopeEnd = " }", bool excludeJsonPunctuations = false)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    VisitObject(element, scopeStart, scopeEnd, excludeJsonPunctuations);
                    break;
                case JsonValueKind.Array:
                    VisitArray(element, excludeJsonPunctuations: excludeJsonPunctuations);
                    break;
                case JsonValueKind.False:
                case JsonValueKind.Null:
                case JsonValueKind.Number:
                case JsonValueKind.String:
                case JsonValueKind.True:
                case JsonValueKind.Undefined:
                    VisitLiteral(element, excludeJsonPunctuations: excludeJsonPunctuations);
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
        private void VisitObject(JsonElement obj, string scopeStart = "{", string scopeEnd = "}", bool excludeJsonPunctuations = false)
        {
            bool multiLine = AlwaysMultiLine(obj);
            if (excludeJsonPunctuations)
            {
                scopeStart = scopeEnd = String.Empty;
            }
            using (this.Writer.Scope(scopeStart, scopeEnd, multiLine))
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
                    if (!excludeJsonPunctuations)
                        this.Writer.Write(CodeFileTokenKind.Punctuation, "\"");
                    
                    var propertyType = isCollapsible ? CodeFileTokenKind.TypeName : CodeFileTokenKind.MemberName;
                    this.Writer.Write(propertyType, property.Name);


                    // Create an ID for this property
                    string id = this.iteratorPath.CurrentPath();
                    this.Writer.AnnotateDefinition(id);
                    if (isCollapsible)
                    {
                        this.Writer.Write(CodeFileTokenKind.FoldableSectionHeading, id);
                    }

                    // Visit the value
                    if (isCollapsible)
                    {
                        var punctuation = excludeJsonPunctuations ? ": " : "\": ";
                        this.Writer.Write(CodeFileTokenKind.Punctuation, punctuation);

                        this.Writer.WriteLine();
                        this.Writer.Write(CodeFileTokenKind.FoldableSectionHeading, null);
                        Visit(property.Value, excludeJsonPunctuations: excludeJsonPunctuations);
                        if (property.Name != values.Last().Name)
                        {
                            if (!excludeJsonPunctuations)
                                this.Writer.Write(CodeFileTokenKind.Punctuation, ", ");

                            if (multiLine) { this.Writer.WriteLine(); }
                        }

                        this.Writer.Write(CodeFileTokenKind.FoldableSectionHeading, null);
                    }
                    else
                    {
                        var punctuation = excludeJsonPunctuations ? ": " : "\": ";
                        this.Writer.Write(CodeFileTokenKind.Punctuation, punctuation);

                        Visit(property.Value, excludeJsonPunctuations: excludeJsonPunctuations);
                        if (property.Name != values.Last().Name)
                        {
                            if (!excludeJsonPunctuations)
                                this.Writer.Write(CodeFileTokenKind.Punctuation, ", ");

                            if (multiLine) { this.Writer.WriteLine(); }
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
        private void VisitArray(JsonElement array, string scopeStart = "[ ", string scopeEnd = " ]", bool excludeJsonPunctuations = false)
        {
            bool multiLine = AlwaysMultiLine(array);
            if (excludeJsonPunctuations)
            {
                scopeStart = scopeEnd = String.Empty;
            }
            using (this.Writer.Scope(scopeStart, scopeEnd, multiLine))
            {
                int index = 0;
                SwaggerTokenSerializer.Fenceposter fencepost = new();

                foreach (JsonElement child in array.EnumerateArray())
                {
                    if (fencepost.RequiresSeparator)
                    {
                        this.Writer.Write(CodeFileTokenKind.Punctuation, ", ");
                        if (multiLine) { this.Writer.WriteLine(); }
                    }

                    this.iteratorPath.Add(index.ToString());
                    Visit(child, excludeJsonPunctuations: excludeJsonPunctuations);
                    this.iteratorPath.Pop();
                    index++;
                }
            }
        }

        /// <summary>
        /// Generate the listing for a literal value.
        /// </summary>
        /// <param name="value">The literal value.</param>
        private void VisitLiteral(JsonElement value, bool excludeJsonPunctuations = false)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Null:
                    this.Writer.Write(CodeFileTokenKind.Keyword, "null");
                    break;
                case JsonValueKind.Undefined:
                    this.Writer.Write(CodeFileTokenKind.Keyword, "undefined");
                    break;
                case JsonValueKind.True:
                    this.Writer.Write(CodeFileTokenKind.Keyword, "true");
                    break;
                case JsonValueKind.False:
                    this.Writer.Write(CodeFileTokenKind.Keyword, "false");
                    break;
                case JsonValueKind.Number:
                    this.Writer.Write(CodeFileTokenKind.Literal, value.GetDouble().ToString());
                    break;
                case JsonValueKind.String:
                    if (!excludeJsonPunctuations)
                        this.Writer.Write(CodeFileTokenKind.Punctuation, "\"");
                    this.Writer.Write(CodeFileTokenKind.StringLiteral, value.GetString());
                    this.iteratorPath.Add(value.GetString());
                    this.Writer.AnnotateDefinition(this.iteratorPath.CurrentPath());
                    this.iteratorPath.Pop();
                    if (!excludeJsonPunctuations)
                        this.Writer.Write(CodeFileTokenKind.Punctuation, "\"");
                    break;
                default:
                    throw new InvalidOperationException($"Expected a literal JSON element, not {value.ValueKind}.");
            }
        }

        private bool AlwaysMultiLine(JsonElement element)
        {
            return true;
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
