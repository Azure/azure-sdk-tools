using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SwaggerApiParser.SwaggerApiView
{
    public class CodeFile
    {
        private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions() { AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip };

        private static HashSet<string> _collapsibleLanguages = new HashSet<string>(new string[] { "Swagger" });


        public string VersionString { get; set; }

        public string Name { get; set; }

        public string Language { get; set; }

        public string LanguageVariant { get; set; }

        public string PackageName { get; set; }

        public string ServiceName { get; set; }

        public string PackageDisplayName { get; set; }

        public string PackageVersion { get; set; }

        public CodeFileToken[] Tokens { get; set; } = Array.Empty<CodeFileToken>();

        public List<CodeFileToken[]> LeafSections { get; set; }

        public NavigationItem[] Navigation { get; set; }

        public static bool IsCollapsibleSectionSSupported(string language) => _collapsibleLanguages.Contains(language);

        public static async Task<CodeFile> DeserializeAsync(Stream stream, bool hasSections = false)
        {
            var codeFile = await JsonSerializer.DeserializeAsync<CodeFile>(
                stream,
                JsonSerializerOptions);

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
            await JsonSerializer.SerializeAsync(
                stream,
                this,
                JsonSerializerOptions);
        }
    }
}
