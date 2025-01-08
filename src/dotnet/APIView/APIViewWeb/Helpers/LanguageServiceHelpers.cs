using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;

namespace APIViewWeb.Helpers
{
    public class LanguageServiceHelpers
    {
        public static string[] SupportedLanguages = new string[] { "C", "C#", "C++", "Go", "Java", "JavaScript", "Json", "Kotlin", "Python", "Swagger", "Swift", "TypeSpec", "Xml" };

        public static IEnumerable<string> MapLanguageAliases(IEnumerable<string> languages)
        {
            HashSet<string> result = new HashSet<string>();

            foreach (var language in languages)
            {
                if (language.Equals("TypeSpec") || language.Equals("Cadl"))
                {
                    result.Add("Cadl");
                    result.Add("TypeSpec");
                }
                result.Add(language);
            }

            return result.ToList();
        }

        public static string MapLanguageAlias(string language)
        {
            if (language.Equals("net") || language.Equals(".NET"))
                return "C#";

            if (language.Equals("cpp"))
                return "C++";

            if (language.Equals("js"))
                return "JavaScript";

            if (language.Equals("Cadl"))
                return "TypeSpec";

            return language;
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

            return result;
        }

        public static LanguageService GetLanguageService(string language, IEnumerable<LanguageService> languageServices)
        {
            return languageServices.FirstOrDefault(service => service.Name == language);
        }

        public static bool UseLineBreakForParameterSeparator(string language)
        { 
            switch(language)
            {
                case "Python":
                    return true;
                default:
                    return false;
            }
        }

        public static bool UseTreeStyleParser(string language)
        {
            switch (language)
            {
                case "C#":
                case "JavaScript":
                    return true;
                default:
                    return false;
            }
        }
    }
}
