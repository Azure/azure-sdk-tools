// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIView;
using APIView.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ApiView
{
    public class CodeFile
    {
        private static readonly JsonSerializer JsonSerializer = new JsonSerializer()
        {
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
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

        public string CodeFileVersion
        {
            get => this.APIForest.Count > 0 ? "v2" : "v1";
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
        public List<APITreeNodeForAPI> APIForest { get; set; } = new List<APITreeNodeForAPI>();

        public List<CodeFileToken[]> LeafSections { get; set; }

        public NavigationItem[] Navigation { get; set; }

        public CodeDiagnostic[] Diagnostics { get; set; }

        public override string ToString()
        {
            return new CodeFileRenderer().Render(this).CodeLines.ToString();
        }
        
        public static bool IsCollapsibleSectionSSupported(string language) => _collapsibleLanguages.Contains(language);

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public static async Task<CodeFile> DeserializeAsync(Stream stream, bool hasSections = false)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            CodeFile codeFile = null;
            var converter = new StructuredTokenConverter();
            JsonSerializer.Converters.Add(converter);

            using (var streamReader = new StreamReader(stream, leaveOpen: true))
            {
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    jsonReader.SupportMultipleContent = true;
                    codeFile =  JsonSerializer.Deserialize<CodeFile>(jsonReader);
                }
            }

            if (hasSections == false && codeFile.LeafSections == null && IsCollapsibleSectionSSupported(codeFile.Language))
                hasSections = true;

            // Spliting out the 'leafSections' of the codeFile is done so as not to have to render large codeFiles at once
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
            using (var streamWriter = new StreamWriter(stream, leaveOpen: true))
            {
                using (var jsonWriter = new JsonTextWriter(streamWriter))
                {
                    JsonSerializer.Serialize(jsonWriter, this);
                    await jsonWriter.FlushAsync();
                }
            }
        }
    }
}
