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
using System.Text.RegularExpressions;
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

        public string GetApiText()
        {
            StringBuilder sb = new();
            foreach (var line in ReviewLines)
            {
                line.GetApiText(sb, 0, true);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Generates a complete text representation of API surface to help generating the content.
        /// One use case of this function will be to support download request of entire API review surface.
        /// </summary>
        public string GetApiText()
        {
            StringBuilder sb = new();
            foreach (var line in ReviewLines)
            {
                line.AppendApiTextToBuilder(sb, 0, true);
            }
            return sb.ToString();
        }       
    }
}
