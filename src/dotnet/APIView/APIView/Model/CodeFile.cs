// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIView;
using APIView.Model.V2;
using APIView.TreeToken;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ApiView
{
    public class CodeFile
    {
        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private string _versionString;
        private static HashSet<string> _collapsibleLanguages = new HashSet<string>(new string[] { "Swagger" });
        [Obsolete("This is only for back compat, VersionString should be used")]
        public int Version { get; set; }
        public string VersionString
        {
#pragma warning disable 618
            get => _versionString ?? Version.ToString();
#pragma warning restore 618
            set => _versionString = value;
        }
        public string Name { get; set; }
        public string Language { get; set; }
        public string LanguageVariant { get; set; }
        public string PackageName { get; set; }
        public string ServiceName { get; set; }
        public string PackageDisplayName { get; set; }
        public string PackageVersion { get; set; }
        public string CrossLanguagePackageId { get; set; }
        public CodeFileToken[] Tokens { get; set; } = Array.Empty<CodeFileToken>();
        // APIForest will be removed once server changes are added to dereference this property
        public List<APITreeNode> APIForest { get; set; } = new List<APITreeNode>();
        public List<CodeFileToken[]> LeafSections { get; set; }
        public NavigationItem[] Navigation { get; set; }
        public CodeDiagnostic[] Diagnostics { get; set; }
        public string ParserVersion
        {
            get => _versionString;
            set => _versionString = value;
        }
        public List<ReviewLine> ReviewLines { get; set; } = [];

        public override string ToString()
        {
            return new CodeFileRenderer().Render(this).CodeLines.ToString();
        }  
        public static bool IsCollapsibleSectionSSupported(string language) => _collapsibleLanguages.Contains(language);

        public static async Task<CodeFile> DeserializeAsync(Stream stream, bool hasSections = false, bool doTreeStyleParserDeserialization = false)
        {
            var codeFile = await JsonSerializer.DeserializeAsync<CodeFile>(stream, _serializerOptions);

            if (hasSections == false && codeFile.LeafSections == null && IsCollapsibleSectionSSupported(codeFile.Language))
                hasSections = true;

            // Splitting out the 'leafSections' of the codeFile is done so as not to have to render large codeFiles at once
            // Rendering sections in part helps to improve page load time
            if (hasSections)
            {
                var index = 0;
                var tokens = codeFile.Tokens;
                var newTokens = new List<CodeFileToken>();
                var leafSections = new List<CodeFileToken[]>();
                var section = new List<CodeFileToken>();
                var isLeaf = false;
                var numberOfLinesinLeafSection = 0;

                while (index < tokens.Length)
                {
                    var token = tokens[index];
                    if (token.Kind == CodeFileTokenKind.FoldableSectionHeading)
                    {
                        section.Add(token);
                        isLeaf = false;
                    }
                    else if (token.Kind == CodeFileTokenKind.FoldableSectionContentStart)
                    {
                        section.Add(token);
                        newTokens.AddRange(section);
                        section.Clear();
                        isLeaf = true;
                        numberOfLinesinLeafSection = 0;
                    }
                    else if (token.Kind == CodeFileTokenKind.FoldableSectionContentEnd)
                    {
                        if (isLeaf)
                        {
                            leafSections.Add(section.ToArray());
                            section.Clear();
                            isLeaf = false;

                            // leafSectionPlaceholder will be used to identify the appriopriate index and number of lines in the leafSections
                            // numberOfLinesinLeafSection help keep line numbering consistent with the main 'non-leaf' sections
                            var leafSectionPlaceholder = new CodeFileToken(
                                $"{(leafSections.Count() - 1)}", CodeFileTokenKind.LeafSectionPlaceholder, numberOfLinesinLeafSection);
                            var newLineToken = new CodeFileToken("", CodeFileTokenKind.Newline);
                            section.Add(leafSectionPlaceholder);
                            section.Add(newLineToken);
                        }
                        section.Add(token);
                    }
                    else
                    {
                        if (isLeaf && token.Kind == CodeFileTokenKind.Newline)
                        {
                            numberOfLinesinLeafSection++;
                        }

                        if (isLeaf && token.Kind == CodeFileTokenKind.TableRowCount)
                        {
                            numberOfLinesinLeafSection += (Convert.ToInt16(token.Value)) + 1;
                        }

                        section.Add(token);
                    }
                    index++;
                }
                newTokens.AddRange(section);
                codeFile.Tokens = newTokens.ToArray();
                codeFile.LeafSections = leafSections;
            }
            return codeFile;
        }

        public async Task SerializeAsync(Stream stream)
        {
            await JsonSerializer.SerializeAsync(stream, this, _serializerOptions);
        }

        /// <summary>
        /// Generates a complete text representation of API surface to help generating the content.
        /// One use case of this function will be to support download request of entire API review surface.
        /// </summary>
        public string GetApiText(bool skipDocs = true)
        {
            StringBuilder sb = new();
            foreach (var line in ReviewLines)
            {
                line.AppendApiTextToBuilder(sb, 0, skipDocs, GetIndentationForLanguage(Language));
            }
            return sb.ToString();
        }

        public static int GetIndentationForLanguage(string language)
        {
            switch (language)
            {
                case "C++":
                case "C":
                    return 2;
                default:
                    return 4;
            }
        }

        public void ConvertToTreeTokenModel()
        {
            Dictionary<string, string> navigationItems = new Dictionary<string, string>();
            ReviewLine reviewLine = new ReviewLine();
            ReviewLine previousLine = null;
            bool isDocumentation = false;
            bool isHidden = false;
            bool skipDiff = false;
            bool isDeprecated = false;
            bool skipIndent = false;
            string className = "";
            //Process all navigation items in old model to generate a map
            GetNavigationMap(navigationItems, Navigation);

            List<ReviewToken> currentLineTokens = new List<ReviewToken>();
            foreach(var oldToken in Tokens)
            {
                //Don't include documentation in converted code file due to incorrect documentation formatting used in previous model.
                if (isDocumentation && oldToken.Kind != CodeFileTokenKind.DocumentRangeEnd)
                    continue;
                ReviewToken token = null;
                switch(oldToken.Kind)
                {
                    case CodeFileTokenKind.DocumentRangeStart:
                        isDocumentation = true; break;
                    case CodeFileTokenKind.DocumentRangeEnd:
                        isDocumentation = false; break;
                    case CodeFileTokenKind.DeprecatedRangeStart:
                        isDeprecated = true; break;
                    case CodeFileTokenKind.DeprecatedRangeEnd:
                        isDeprecated = false; break;
                    case CodeFileTokenKind.SkipDiffRangeStart:
                        skipDiff = true; break;
                    case CodeFileTokenKind.SkipDiffRangeEnd:
                        skipDiff = false; break;
                    case CodeFileTokenKind.HiddenApiRangeStart:
                        isHidden = true; break;
                    case CodeFileTokenKind.HiddenApiRangeEnd:
                        isHidden = false; break;
                    case CodeFileTokenKind.Keyword:
                        token = ReviewToken.CreateKeywordToken(oldToken.Value, false);
                        var keywordValue = oldToken.Value.ToLower();
                        if (keywordValue == "class" || keywordValue == "enum" || keywordValue == "struct" || keywordValue == "interface" || keywordValue == "type" || keywordValue == "namespace")
                            className = keywordValue;
                        break;
                    case CodeFileTokenKind.Comment:
                        token = ReviewToken.CreateCommentToken(oldToken.Value, false); 
                        break;
                    case CodeFileTokenKind.Text:
                        token = ReviewToken.CreateTextToken(oldToken.Value, oldToken.NavigateToId, false); 
                        break;
                    case CodeFileTokenKind.Punctuation:
                        token = ReviewToken.CreatePunctuationToken(oldToken.Value, false); 
                        break;
                    case CodeFileTokenKind.TypeName:
                        token = ReviewToken.CreateTypeNameToken(oldToken.Value, false);
                        if (currentLineTokens.Any(t => t.Kind == TokenKind.Keyword && t.Value.ToLower() == className))
                            token.RenderClasses.Add(className);
                        className = "";
                        break;
                    case CodeFileTokenKind.MemberName:
                        token = ReviewToken.CreateMemberNameToken(oldToken.Value, false); 
                        break;
                    case CodeFileTokenKind.StringLiteral:
                        token = ReviewToken.CreateStringLiteralToken(oldToken.Value, false); 
                        break;
                    case CodeFileTokenKind.Literal:
                        token = ReviewToken.CreateLiteralToken(oldToken.Value, false); 
                        break;
                    case CodeFileTokenKind.ExternalLinkStart:
                        token = ReviewToken.CreateStringLiteralToken(oldToken.Value, false); 
                        break;
                    case CodeFileTokenKind.Whitespace:
                        if (currentLineTokens.Count > 0) {
                            currentLineTokens.Last().HasSuffixSpace = true;
                        }
                        else if (!skipIndent) {
                            reviewLine.Indent += oldToken.Value.Length;
                        }
                        break;
                    case CodeFileTokenKind.Newline:
                        var parent = previousLine;
                        skipIndent = false;
                        if (currentLineTokens.Count > 0)
                        {
                            while (parent != null && parent.Indent >= reviewLine.Indent)
                                parent = parent.parentLine;
                        }
                        else
                        {
                            //If current line is empty line then add it as an empty line under previous line's parent
                            parent = previousLine?.parentLine;
                        }
                        
                        if (parent == null)
                        {
                            this.ReviewLines.Add(reviewLine);
                        }
                        else
                        {
                            parent.Children.Add(reviewLine);
                            reviewLine.parentLine = parent;
                        }

                        //Handle specific cases for C++ line with 'public:' and '{' to mark related line
                        if ((currentLineTokens.Count == 1 && currentLineTokens.First().Value == "{") ||
                            (currentLineTokens.Count == 2 && currentLineTokens.Any(t => t.Kind == TokenKind.Keyword && t.Value == "public")))
                        {
                            reviewLine.RelatedToLine = previousLine?.LineId;
                        }

                        if (currentLineTokens.Count == 0)
                        {
                            //Empty line. So just add previous line as related line
                            reviewLine.RelatedToLine = previousLine?.LineId;
                        }
                        else
                        {
                            reviewLine.Tokens = currentLineTokens;
                            previousLine = reviewLine;
                        }

                        reviewLine = new ReviewLine();
                        // If previous line ends with "," then next line will be sub line to show split content in multiple lines. 
                        // Set next line's indent same as current line
                        // This is required to convert C++ tokens correctly
                        if (previousLine != null && previousLine.Tokens.LastOrDefault()?.Value == "," && Language == "C++")
                        {
                            reviewLine.Indent = previousLine.Indent;
                            skipIndent = true;
                        }
                        currentLineTokens = new List<ReviewToken>();
                        break;
                    case CodeFileTokenKind.LineIdMarker:
                        if (string.IsNullOrEmpty(reviewLine.LineId))
                            reviewLine.LineId = oldToken.Value;
                        break;
                    default:
                        Console.WriteLine($"Unsupported token kind to convert to new model, Kind: {oldToken.Kind}, value: {oldToken.Value}, Line Id: {oldToken.DefinitionId}"); 
                        break;
                }

                if (token != null)
                {
                    currentLineTokens.Add(token);

                    if (oldToken.Equals("}") || oldToken.Equals("};"))
                        reviewLine.IsContextEndLine = true;
                    if (isHidden)
                        reviewLine.IsHidden = true;
                    if (oldToken.DefinitionId != null)
                        reviewLine.LineId = oldToken.DefinitionId;
                    if (oldToken.CrossLanguageDefinitionId != null)
                        reviewLine.CrossLanguageId = oldToken.CrossLanguageDefinitionId;
                    if (isDeprecated)
                        token.IsDeprecated = true;
                    if (skipDiff)
                        token.SkipDiff = true;
                    if (isDocumentation)
                        token.IsDocumentation = true;
                }
            }

            //Process last line
            if (currentLineTokens.Count > 0)
            {                
                reviewLine.Tokens = currentLineTokens;
                var parent = previousLine;
                while (parent != null && parent.Indent >= reviewLine.Indent)
                    parent = parent.parentLine;

                if (parent == null)
                    this.ReviewLines.Add(reviewLine);
                else
                    parent.Children.Add(reviewLine);
            }                        
        }

        private static void GetNavigationMap(Dictionary<string, string> navigationItems, NavigationItem[] items)
        {
            if (items == null)
                return;

            foreach (var item in items)
            {
                var key = string.IsNullOrEmpty(item.NavigationId) ? item.Text : item.NavigationId;
                navigationItems.Add(key, item.Text);
                GetNavigationMap(navigationItems, item.ChildItems);
            }
        }
    }
}
