using System;
using System.Collections.Generic;
using System.Linq;

namespace APIViewWeb.Helpers
{
    public class LanguageServiceHelpers
    {
        public static string[] SupportedLanguages = ["C", "C#", "C++", "Go", "Java", "JavaScript", "Json", "Kotlin", "Python", "Rust", "Swagger", "Swift", "TypeSpec", "Xml"];

        public static IEnumerable<string> MapLanguageAliases(IEnumerable<string> languages)
        {
            HashSet<string> result = new HashSet<string>();

            foreach (var language in languages)
            {
                if (language.Equals(ApiViewConstants.TypeSpecLanguage) || language.Equals("Cadl"))
                {
                    result.Add("Cadl");
                    result.Add(ApiViewConstants.TypeSpecLanguage);
                }
                result.Add(language);
            }

            return result.ToList();
        }

        public static string MapLanguageAlias(string language)
        {
            if (language.Equals("net", StringComparison.OrdinalIgnoreCase) || language.Equals(".NET", StringComparison.OrdinalIgnoreCase) ||
                language.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                return "C#";

            if (language.Equals("csharp", StringComparison.OrdinalIgnoreCase))
                return "C#";

            if (language.Equals("cpp", StringComparison.OrdinalIgnoreCase))
                return "C++";

            if (language.Equals("js", StringComparison.OrdinalIgnoreCase) || language.Equals("typescript", StringComparison.OrdinalIgnoreCase) ||
                language.Equals("ts", StringComparison.OrdinalIgnoreCase))
                return "JavaScript";

            if (language.Equals("Cadl", StringComparison.OrdinalIgnoreCase))
                return ApiViewConstants.TypeSpecLanguage;

            return SupportedLanguages.FirstOrDefault(lang => lang.Equals(language, StringComparison.OrdinalIgnoreCase)) ?? language;
        }

        public static string GetLanguageAliasForCopilotService(string language, string languageVariant = null)
        {
            if (string.IsNullOrEmpty(language))
            {
                return null;
            }

            return language.ToLowerInvariant() switch
            {
                "c" => "clang",
                "c#" or "cs" or "csharp" or "dotnet" or ".net" or "net" => "dotnet",
                "c++" or "cpp" => "cpp",
                "javascript" or "js" => "typescript",
                "typescript" or "ts" => "typescript",
                "swift" => "ios",
                "go" or "golang" => "golang",
                "java" => languageVariant == "Android" ? "android" : "java",
                "py" or "python" => "python",
                "rust" => "rust",
                _ => language.ToLowerInvariant()
            };
        }

        public static string GetLanguageFromRepoName(string repoName)
        {
            var result = String.Empty;

            if (repoName.EndsWith("-net"))
                result = "C#";
            if (repoName.EndsWith("-c"))
                result = "C";
            if (repoName.EndsWith("-cpp"))
                result = "C++";
            if (repoName.EndsWith("-go"))
                result = "Go";
            if (repoName.EndsWith("-java"))
                result = "Java";
            if (repoName.EndsWith("-js"))
                result = "JavaScript";
            if (repoName.EndsWith("-python"))
                result = "Python";
            if (repoName.EndsWith("-ios"))
                result = "Swift";
            if(repoName.EndsWith("-rust"))
                result = "Rust";

            return result;
        }

        public static LanguageService GetLanguageService(string language, IEnumerable<LanguageService> languageServices)
        {
            return languageServices.FirstOrDefault(service => service.Name.Equals(language, StringComparison.OrdinalIgnoreCase));
        }
    }
}
