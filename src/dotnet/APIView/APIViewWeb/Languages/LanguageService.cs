using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ApiView;
using APIView;
using APIView.Model.V2;
using APIViewWeb.Helpers;
using APIViewWeb.Models;

namespace APIViewWeb
{
    public abstract class LanguageService
    {
        public abstract string Name { get; }
        public abstract string [] Extensions { get; }
        public abstract string VersionString { get; }
        public virtual bool IsSupportedFile(string name) => Extensions.Any(x => name.EndsWith(x, StringComparison.OrdinalIgnoreCase));
        public abstract bool CanUpdate(string versionString);
        public abstract Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis, string crossLanguageMetadata = null);
        public virtual bool IsReviewGenByPipeline { get; set; } = false;
        public virtual bool UsesTreeStyleParser { get; } = true;
        public virtual string ReviewGenerationPipelineUrl { get; } = String.Empty;
        public virtual CodeFile GetReviewGenPendingCodeFile(string fileName) 
        {
            var line1 = "API review is being generated for this revision and it will be available in few minutes.";
            var line2 = "Please refresh this page after few minutes to see generated API review.";
            var line3 = $"See {ReviewGenerationPipelineUrl}";

            var codeFile = new CodeFile()
            {
                Name = fileName,
                PackageName = fileName,
                Language = Name,
                Navigation = new NavigationItem[] { new NavigationItem() { Text = fileName } },
                ContentGenerationInProgress = true
            };

            if (UsesTreeStyleParser)
            {
                codeFile.ReviewLines = new List<ReviewLine>
                {
                    new ReviewLine
                    {
                        Tokens = new List<ReviewToken>
                        {
                            new ReviewToken("", TokenKind.Literal)
                        }
                    },
                    new ReviewLine
                    {
                        Tokens = new List<ReviewToken>
                        {
                            new ReviewToken(line1, TokenKind.Literal)
                        }
                    },
                    new ReviewLine
                    {
                        Tokens = new List<ReviewToken>
                        {
                            new ReviewToken(line2, TokenKind.Literal)
                        }
                    }
                };

                if (!string.IsNullOrEmpty(ReviewGenerationPipelineUrl))
                {
                    codeFile.ReviewLines.Add(
                        new ReviewLine
                        {
                            Tokens = new List<ReviewToken>
                            {
                                new ReviewToken(line3, TokenKind.Literal)
                            }
                        }
                    );
                }
            }
            else
            {
                var tokens  = new List <CodeFileToken> {
                    new CodeFileToken("", CodeFileTokenKind.Newline),
                    new CodeFileToken(line1, CodeFileTokenKind.Literal),
                    new CodeFileToken(line2, CodeFileTokenKind.Literal),
                };
                if (!string.IsNullOrEmpty(ReviewGenerationPipelineUrl))
                {
                    tokens.Add(new CodeFileToken(line3, CodeFileTokenKind.Literal));
                }
                codeFile.Tokens = tokens.ToArray();
            }
            return codeFile;
        }

        public static string[] SupportedLanguages = LanguageServiceHelpers.SupportedLanguages;

        public virtual bool GeneratePipelineRunParams(APIRevisionGenerationPipelineParamModel param) => true;
        public virtual bool CanConvert(string versionString) => false;
    }
}
