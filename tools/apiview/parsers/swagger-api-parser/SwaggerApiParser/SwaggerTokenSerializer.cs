using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SwaggerApiParser.SwaggerApiView;

namespace SwaggerApiParser
{
    public class SwaggerTokenSerializer
    {
        internal const string LanguageServiceName = "Swagger";
        public string Name => LanguageServiceName;

        // This is an unfortunate hack because JsonLanguageService is already
        // squatting on `.json`.  We'll have to fix this before we ask anyone
        // to use ApiView for swagger files.
        public string Extension => ".swagger";


        // I don't really know what this is doing, but the other language
        // services do the same.  It'd probably be worth making this the default
        // implementation if everyone needs to copy it as-is.
        public bool CanUpdate(string versionString) => false;

        public async Task<CodeFile> GetCodeFileInternalAsync(string originalName, Stream stream, bool runAnalysis) =>
            SwaggerVisitor.GenerateCodeListing(originalName, await JsonDocument.ParseAsync(stream));


#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task<CodeFile> GetCodeFileFromJsonDocumentAsync(string originalName, JsonDocument jsonDoc, bool runAnalysis) =>
            SwaggerVisitor.GenerateCodeListing(originalName, jsonDoc);
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously


        /// <summary>
        /// Generate an ApiView listing for an OpenAPI 2.0 specification in
        /// JSON.
        /// </summary>
        /// <param name="originalName">The name of the swagger file.</param>
        /// <param name="stream">Stream full of JSON.</param>
        /// <param name="runAnalysis">This is unused.</param>
        /// <returns>An ApiView listing.</returns>
        public async Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis) =>
            await CodeFile.DeserializeAsync(stream);

        /// <summary>
        /// Incredibly simple class to make fenceposting (i.e., performing an
        /// operation between every element in a sequence but not before or
        /// after) easy and consistent.  
        /// </summary>
        internal class Fenceposter
        {
            /// <summary>
            /// Flag indicating whether a separator is required.  It starts
            /// false, but will be permanently flipped to true after the first
            /// time the RequiresSeperator property is accessed.
            /// </summary>
            private bool _requiresSeparator = false;

            /// <summary>
            /// Gets a value indicating whether a separator is required.  This
            /// will always be false the first time it is called after a new
            /// Fenceposter is constructed and true every time after.
            /// </summary>
            public bool RequiresSeparator
            {
                get
                {
                    if (_requiresSeparator)
                    {
                        return true;
                    }
                    else
                    {
                        _requiresSeparator = true;
                        return false;
                    }
                }
            }
        }

        /// <summary>
        /// IndentWriter provides helpful features for writing blocks of indented
        /// text (like source code, JSON, etc.).
        /// </summary>
        public partial class IndentWriter
        {
            /// <summary>
            /// The buffer where tokens are written.  It is obtained by the
            /// user via IndentWriter.ToTokens().
            /// </summary>
            private IList<CodeFileToken> _tokens = new List<CodeFileToken>();

            /// <summary>
            /// Whether or not the last character written was a newline
            /// character (which means the next line written should
            /// automatically add the current indent depth).
            /// </summary>        
            private bool _isNewline = true;

            /// <summary>
            /// Gets or sets the text used as each indent character (i.e.,
            /// could be a single tab character or four space characters).  The
            /// default value is four space characters.
            /// </summary>
            public string IndentText { get; set; } = "  ";

            /// <summary>
            /// Gets the depth of the current indent level.
            /// </summary>
            public uint Indent { get; private set; }

            /// <summary>
            /// Gets the text that has been written thus far.
            /// </summary>
            /// <returns>The text written thus far.</returns>
            public CodeFileToken[] ToTokens() => _tokens.ToArray();

            private int GetLastTokenIndex(CodeFileTokenKind? kind = null)
            {
                CodeFileToken last;
                for (int i = _tokens.Count - 1; i >= 0; i--)
                {
                    last = _tokens[i];
                    if (kind == null || last.Kind == kind) { return i; }
                }

                return -1;
            }

            public void AnnotateDefinition(string definitionId)
            {
                int index = GetLastTokenIndex();
                var last = _tokens[index];
                last.DefinitionId = definitionId;
                _tokens[index] = last;
            }

            public void AnnotateLink(string navigationId, CodeFileTokenKind kind)
            {
                int index = GetLastTokenIndex(kind);
                var last = _tokens[index];
                last.NavigateToId = navigationId;
                _tokens[index] = last;
            }

            /// <summary>
            /// Pushes the scope a level deeper.
            /// </summary>
            public void PushScope() => Indent++;

            /// <summary>
            /// Pops the scope a level.
            /// </summary>
            public void PopScope()
            {
                if (Indent == 0)
                {
                    throw new InvalidOperationException("Cannot pop scope any further!");
                }

                Indent--;
            }

            private void Append(CodeFileTokenKind kind, string text) =>
                _tokens.Add(new CodeFileToken(text, kind));

            /// <summary>
            /// Writes an indent if needed.  This is used before each write
            /// operation to ensure we're always indenting.  We don't need to
            /// indent for a series of calls like Write("Foo"); Write("Bar");
            /// but would indent between a series like WriteLine("Foo");
            /// Write("Bar");
            /// </summary>
            private void WriteIndentIfNeeded()
            {
                // If we had just written a full line
                if (_isNewline)
                {
                    _isNewline = false;

                    // Then we'll write out the current indent depth before anything
                    // else is written
                    Append(
                        CodeFileTokenKind.Whitespace,
                        string.Concat(Enumerable.Repeat(IndentText, (int)Indent)));
                }
            }

            /// <summary>
            /// Write the text representation of the given values with
            /// indentation as appropriate.
            /// </summary>
            /// <param name='format'>Format string.</param>
            /// <param name='args'>Optional arguments to the format string.</param>
            public void Write(CodeFileTokenKind kind, string format, params object[] args)
            {
                WriteIndentIfNeeded();

                // Only use AppendFormat if we have args so that we don't have
                // to escape curly brace literals used on their own.
                if (args?.Length > 0)
                {
                    format = string.Format(format, args);
                }

                Append(kind, format);
            }

            /// <summary>
            /// Write the text representation of the given values followed by a
            /// newline, with indentation as appropriate.  This will force the
            /// next Write call to indent before anything else is written.
            /// </summary>
            /// <param name='format'>Format string.</param>
            /// <param name='args'>Optional arguments to the format string.</param>
            public void WriteLine(CodeFileTokenKind kind, string format, params object[] args)
            {
                Write(kind, format, args);
                Write(CodeFileTokenKind.Newline, Environment.NewLine);

                // Track that we just wrote a line so the next write operation
                // will indent first
                _isNewline = true;
            }

            /// <summary>
            /// Write a newline (which will force the next write operation to
            /// indent before anything else is written).
            /// </summary>
            public void WriteLine() => WriteLine(CodeFileTokenKind.Text, null);

            /// <summary>
            /// Increase the indent level after writing the text representation
            /// of the given values to the current line.  This would be used
            /// like:
            ///     myIndentWriter.PushScope("{");
            ///     /* Write indented lines here */
            ///     myIndentWriter.PopScope("}");
            /// </summary>
            /// <param name='format'>Format string.</param>
            /// <param name='args'>Optional arguments to the format string.</param>
            public void PushScope(CodeFileTokenKind kind, string format, params object[] args) =>
                PushScope(newline: true, kind, format, args);

            public void PushScope(bool newline, CodeFileTokenKind kind, string format, params object[] args)
            {
                if (newline)
                {
                    WriteLine(kind, format, args);
                }
                else
                {
                    Write(kind, format, args);
                }

                PushScope();
            }

            /// <summary>
            /// Decrease the indent level after writing the text representation
            /// of the given values to the current line.  This would be used
            /// like:
            ///     myIndentWriter.PushScope("{");
            ///     /* Write indented lines here */
            ///     myIndentWriter.PopScope("}");
            /// </summary>
            /// <param name='format'>Format string.</param>
            /// <param name='args'>Optional arguments to the format string.</param>
            public void PopScope(CodeFileTokenKind kind, string format, params object[] args) =>
                PopScope(newline: true, kind, format, args);

            public void PopScope(bool newline, CodeFileTokenKind kind, string format, params object[] args)
            {
                PopScope();

                if (!string.IsNullOrEmpty(format))
                {
                    // Force the format string to be written on a new line, but
                    // don't add an extra one if we just wrote a newline.
                    if (!_isNewline && newline)
                    {
                        WriteLine();
                    }

                    Write(kind, format, args);
                }
            }

            /// <summary>
            /// Create a writer scope that will indent until the scope is
            /// disposed.  This is used like:
            ///     using (writer.Scope())
            ///     {
            ///         /* Write indented lines here */
            ///     }
            ///     /* Back to normal here */
            /// </summary>
            public IDisposable Scope() => new WriterScope(this);


            /// <summary>
            /// Create a writer scope that will indent until the scope is
            /// disposed and starts/ends the scope with the given text.  This
            /// is used like:
            ///     using (writer.Scope("{", "}"))
            ///     {
            ///         /* Write indented lines here */
            ///     }
            ///     /* Back to normal here */
            /// </summary>
            /// <param name='start'>Text starting the scope.</param>
            /// <param name='end'>Text ending the scope.</param>
            /// <param name="kind">The kind of token to use.</param>
            public IDisposable Scope(string start, string end, bool newline = true, CodeFileTokenKind kind = CodeFileTokenKind.Punctuation) =>
                new WriterScope(
                    this,
                    start ?? throw new ArgumentNullException("start"),
                    end ?? throw new ArgumentNullException("end"),
                    newline,
                    kind);

            /// <summary>
            /// The WriterScope class allows us to create an indentation block
            /// via a C# using statement.  It will typically be used via
            /// something like:
            ///     using (writer.Scope("{", "}"))
            ///     {
            ///         /* Indented writing here */
            ///     }
            ///     /* No longer indented from here on... */
            /// </summary>        
            private class WriterScope : IDisposable
            {
                /// <summary>
                /// The IndentWriter that contains this scope.
                /// </summary>
                private IndentWriter _writer;

                /// <summary>
                /// An optional string to write upon closing the scope.
                /// </summary>
                private string _scopeEnd;

                /// <summary>
                /// An optional value indicating whether to add newlines.
                /// </summary>
                private bool _newline;

                /// <summary>
                /// Optional kind of token to write.
                /// </summary>
                private CodeFileTokenKind _kind;

                /// <summary>
                /// Initializes a new instance of the WriterScope class.
                /// </summary>
                /// <param name='writer'>
                /// The IndentWriter containing the scope.
                /// </param>
                /// <param name="kind">The kind of token to write.</param>
                public WriterScope(IndentWriter writer)
                {
                    Debug.Assert(writer != null, "writer cannot be null!");
                    _writer = writer;
                    _writer.PushScope();
                }

                /// <summary>
                /// Initializes a new instance of the WriterScope class.
                /// </summary>
                /// <param name='writer'>
                /// The IndentWriter containing the scope.
                /// </param>
                /// <param name='scopeStart'>Text starting the scope.</param>
                /// <param name='scopeEnd'>Text ending the scope.</param>
                /// <param name="newline">Whether to inject a newline.</param>
                /// <param name="kind">The kind of token to write.</param>
                public WriterScope(IndentWriter writer, string scopeStart, string scopeEnd, bool newline, CodeFileTokenKind kind)
                {
                    Debug.Assert(writer != null, "writer cannot be null!");
                    Debug.Assert(scopeStart != null, "scopeStart cannot be null!");
                    Debug.Assert(scopeEnd != null, "scopeEnd cannot be null!");

                    _writer = writer;
                    _writer.PushScope(newline, kind, scopeStart);
                    _scopeEnd = scopeEnd;
                    _kind = kind;
                    _newline = newline;
                }

                /// <summary>
                /// Close the scope.
                /// </summary>
                public void Dispose()
                {
                    if (_writer != null)
                    {
                        // Close the scope with the desired text if given
                        if (_scopeEnd != null)
                        {
                            _writer.PopScope(_newline, _kind, _scopeEnd);
                        }
                        else
                        {
                            _writer.PopScope();
                        }

                        // Prevent multiple disposals
                        _writer = null;
                    }
                }
            }
        }

        /// <summary>
        /// Represents the navigation tree for a swagger document.
        /// </summary>
        internal class SwaggerTree
        {
            /// <summary>
            /// Gets or sets the display text of the tree.
            /// </summary>
            public string Text { get; set; }

            public string LongText { get; set; }

            /// <summary>
            /// Gets or sets the ID of the document element to navigate to.
            /// </summary>
            public string NavigationId { get; set; }

            /// <summary>
            /// Gets or sets the children of the current node.
            /// </summary>
            public IDictionary<string, SwaggerTree> Children { get; } = new Dictionary<string, SwaggerTree>();

            /// <summary>
            /// Gets a value indicating whether this node is the root.
            /// </summary>
            public bool IsRoot => Parent == null;

            /// <summary>
            /// Gets a value indicating whether this node is a top-level entry.
            /// </summary>
            public bool IsTopLevel =>
                Parent?.IsRoot == true && Text switch
                {
                    "paths" => true,
                    "x-ms-paths" => true,
                    "parameters" => true,
                    "definitions" => true,
                    "responses" => true,
                    _ => false
                };

            /// <summary>
            /// Gets the parent of the current node.
            /// </summary>
            public SwaggerTree Parent { get; private set; }

            /// <summary>
            /// Gets a value indicating whether this node is a path entry.
            /// </summary>
            public bool IsPath =>
                Parent?.Parent?.IsRoot == true &&
                (Parent.Text == "paths" || Parent.Text == "x-ms-paths");

            public bool IsResponses => Text switch
            {
                "responses" => true,
                _ => false
            };

            /// <summary>
            /// Gets a value indicating whether this node's children should be
            /// added to the navigation tree.
            /// </summary>
            public bool HasNavigableChildren => IsRoot || IsTopLevel || IsPath || IsResponses;

            /// <summary>
            /// Add a child to the navigation tree.
            /// </summary>
            /// <param name="name">Display name of the child.</param>
            /// <param name="navigationId">Navigation ID of the child.</param>
            /// <param name="longText"></param>
            /// <returns>The child node.</returns>
            public SwaggerTree Add(string name, string navigationId, string longText = null)
            {
                if (!Children.TryGetValue(name, out SwaggerTree next))
                {
                    Children[name] = next = new SwaggerTree { Text = name, LongText = longText, NavigationId = navigationId, Parent = this };
                }

                return next;
            }

            /// <summary>
            /// Turn the swagger view into an ApiView navigation item.
            /// </summary>
            /// <returns>An ApiView navigation item.</returns>
            private NavigationItem BuildItem() =>
                new() { Text = Text, NavigationId = NavigationId, ChildItems = Children.Values.Select(c => c.BuildItem()).ToArray() };

            /// <summary>
            /// Turn the swagger view into ApiView navigation items.
            /// </summary>
            /// <returns>ApiView navigation items.</returns>
            public NavigationItem[] Build() =>
                BuildItem().ChildItems;
        }

        /// <summary>
        /// Visitor to generate ApiView listings for Swagger documents.
        /// </summary>
        internal class SwaggerVisitor
        {
            private IndentWriter _writer = new();
            private SwaggerTree _nav = new();
            private List<string> _path = new() { "#" };

            public SwaggerVisitor()
            {
            }

            /// <summary>
            /// Generate the ApiView code listing for a swagger document.
            /// </summary>
            /// <param name="originalName">The name of the file.</param>
            /// <param name="document">The swagger document.</param>
            /// <returns>An ApiView CodeFile.</returns>
            public static CodeFile GenerateCodeListing(string originalName, JsonDocument document)
            {
                var navigationIdPrefix = $"{originalName}";
                // Process the document
                SwaggerVisitor visitor = new();
                visitor.Visit(document.RootElement, visitor._nav, navigationIdPrefix);

                // Ensure we're looking at OpenAPI 2.0
                // if (GetString(document, "swagger") != "2.0")
                // {
                //     throw new InvalidOperationException("Only Swagger 2.0 is supported.");
                // }

                // Pull the pieces together into a listing
                return new CodeFile()
                {
                    Language = LanguageServiceName,
                    Name = originalName,
                    PackageName = GetString(document, "info", "title") ?? originalName,
                    Tokens = visitor._writer.ToTokens(),
                    Navigation = visitor._nav.Build()
                };
            }

            public static CodeFileToken[] GenerateCodeFileTokens(JsonDocument document)
            {
                SwaggerVisitor visitor = new();
                visitor.Visit(document.RootElement, visitor._nav);
                return visitor._writer.ToTokens();
            }

            /// <summary>
            /// Generate the listing for a JSON value.
            /// </summary>
            /// <param name="element">The JSON value.</param>
            /// <param name="nav">Optional document navigation info.</param>
            /// <param name="navigationIdPrefix"></param>
            /// <param name="scopeStart"></param>
            /// <param name="scopeEnd"></param>
            private void Visit(JsonElement element, SwaggerTree nav = null, string navigationIdPrefix = "", string scopeStart = "{ ", string scopeEnd = " }")
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.Object:
                        VisitObject(element, nav, navigationIdPrefix, scopeStart, scopeEnd);
                        break;
                    case JsonValueKind.Array:
                        VisitArray(element, nav, navigationIdPrefix);
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
            /// <param name="nav">Optional document navigation info.</param>
            /// <param name="navigationIdPrefix"></param>
            /// <param name="scopeStart"></param>
            /// <param name="scopeEnd"></param>
            private void VisitObject(JsonElement obj, SwaggerTree nav, string navigationIdPrefix, string scopeStart = "{", string scopeEnd = "}")
            {
                bool multiLine = !FitsOnOneLine(obj);

                using (_writer.Scope(scopeStart, scopeEnd, multiLine))
                {
                    // Optionally sort the values
                    IEnumerable<JsonProperty> values = obj.EnumerateObject();
                    if (nav?.IsRoot != true)
                    {
                        values = nav?.IsPath == true ? values.OrderBy(p => GetString(p.Value, "operationId") ?? p.Name, new OperationComparer()) : values.OrderBy(p => p.Name);
                    }

                    Fenceposter fencepost = new();

                    // Generate the listing for each property
                    foreach (JsonProperty property in values)
                    {
                        Boolean IsCurObjCollapsible()
                        {
                            bool isPathScope = nav is { Text: "paths" or "x-ms-paths" };
                            bool isMethod = nav is { IsPath: true };
                            bool isDefinition = nav is { Text: "definitions" };
                            bool isMethodParameters = nav is { Parent: { IsPath: true } } && property.Name == "parameters";
                            bool isXmsExamples = nav is { Parent: { IsPath: true } } && property.Name == "x-ms-examples";
                            bool isResponses = nav is { Text: "responses" };
                            bool isParameters = nav is { Text: "parameters" };
                            bool isSecurityDefinitions = nav is { Text: "securityDefinitions" };
                            return isPathScope || isDefinition || isParameters || isSecurityDefinitions;
                        }

                        // Add the property to the current path
                        _path.Add(property.Name);

                        var isCollapsible = IsCurObjCollapsible();
                        // Write the property name
                        _writer.Write(CodeFileTokenKind.Punctuation, "\"");
                        var propertyType = isCollapsible ? CodeFileTokenKind.TypeName : CodeFileTokenKind.MemberName;
                        _writer.Write(propertyType, property.Name);


                        // Create an ID for this property

                        var idPrefix = navigationIdPrefix.Length == 0 ? "" : $"{navigationIdPrefix}_";

                        string id = $"{idPrefix}{string.Join('-', _path).TrimStart('#')}";
                        _writer.AnnotateDefinition(id);
                        if (isCollapsible)
                        {
                            _writer.Write(CodeFileTokenKind.FoldableSectionHeading, id);
                        }

                        // Optionally add a navigation tree node
                        SwaggerTree next = null;
                        if (nav?.HasNavigableChildren == true || (nav?.Parent.IsPath == true && property.Name is "responses" or "parameters"))
                        {
                            string name = property.Name;
                            string longText = property.Name;
                            if (nav.IsPath)
                            {
                                name = GetString(property.Value, "operationId") ?? name;
                            }

                            next = nav.Add(name, id, null);
                        }

                        // Visit the value
                        if (isCollapsible)
                        {
                            _writer.Write(CodeFileTokenKind.Punctuation, "\": ");
                            this._writer.WriteLine();
                            this._writer.Write(CodeFileTokenKind.FoldableSectionContentStart, null);
                            Visit(property.Value, next, navigationIdPrefix);
                            if (property.Name != values.Last().Name)
                            {
                                _writer.Write(CodeFileTokenKind.Punctuation, ", ");
                                if (multiLine) { _writer.WriteLine(); }
                            }
                            this._writer.Write(CodeFileTokenKind.FoldableSectionContentEnd, null);
                        }
                        else
                        {
                            _writer.Write(CodeFileTokenKind.Punctuation, "\": ");
                            Visit(property.Value, next, navigationIdPrefix);
                            if (property.Name != values.Last().Name)
                            {
                                _writer.Write(CodeFileTokenKind.Punctuation, ", ");
                                if (multiLine) { _writer.WriteLine(); }
                            }
                        }

                        // Make $refs linked
                        if (property.Name == "$ref" &&
                            property.Value.ValueKind == JsonValueKind.String &&
                            property.Value.GetString().StartsWith("#/")) // Ignore external docs
                        {
                            _writer.AnnotateLink($"{idPrefix}{property.Value.GetString().TrimStart('#').Replace('/', '-')}", CodeFileTokenKind.StringLiteral);
                        }

                        // Remove the property from the current path
                        _path.RemoveAt(_path.Count - 1);
                    }
                }
            }

            /// <summary>
            /// Generate the listing for an array.
            /// </summary>
            /// <param name="array">The array.</param>
            /// <param name="navigationIdPrefix"></param>
            /// <param name="scopeStart"></param>
            /// <param name="scopeEnd"></param>
            private void VisitArray(JsonElement array, SwaggerTree nav, string navigationIdPrefix, string scopeStart = "[ ", string scopeEnd = " ]")
            {
                bool multiLine = !FitsOnOneLine(array);
                using (_writer.Scope(scopeStart, scopeEnd, multiLine))
                {
                    int index = 0;
                    Fenceposter fencepost = new();

                    Boolean IsCurObjCollapsible()
                    {
                        bool isParameters = nav is { Text: "parameters" };
                        return isParameters;
                    }

                    ;
                    bool isCollapsible = IsCurObjCollapsible();
                    foreach (JsonElement child in array.EnumerateArray())
                    {
                        if (fencepost.RequiresSeparator)
                        {
                            _writer.Write(CodeFileTokenKind.Punctuation, ", ");
                            if (multiLine) { _writer.WriteLine(); }
                        }

                        _path.Add(index.ToString());
                        Visit(child, null, navigationIdPrefix);
                        _path.RemoveAt(_path.Count - 1);
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

}

