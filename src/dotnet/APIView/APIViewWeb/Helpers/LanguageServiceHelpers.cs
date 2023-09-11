using System.Collections.Generic;
using System.Linq;
using APIViewWeb.LeanModels;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using Microsoft.Identity.Client;
using Microsoft.VisualStudio.Services.Common;

namespace APIViewWeb.Helpers
{
    public class LanguageServiceHelpers
    {
        public static string[] SupportedLanguages = new string[] { "C", "C#", "C++", "Go", "Java", "JavaScript", "Json", "Kotlin", "Python", "Swagger", "Swift", "TypeSpec", "Xml" };

        public static string GetCorrespondingPackageName(string sourceLanguage, string targetlanguage, string packageName)
        {
            if (packageName.Equals("widgetmanagerclient") || packageName.Equals("Contoso.WidgetManager") || packageName.Equals("com.azure:contoso-widgetmanager"))
            {
                var result = string.Empty;
                switch (targetlanguage)
                {
                    case "TypeSpec":
                        result = "Contoso.WidgetManager";
                        break;
                    case "Java":
                        result = "com.azure:contoso-widgetmanager";
                        break;
                    case "Python":
                        result = "widgetmanagerclient";
                        break;
                }
                return result;
            }
            else {
                var result = string.Empty;
                switch (targetlanguage)
                {
                    case "C#":
                        result = "Azure.Identity";
                        break;
                    case "C++":
                        result = "azure-identity-cpp";
                        break;
                    case "Go":
                        result = "azidentity";
                        break;
                    case "Java":
                        result = "com.azure:azure-identity";
                        break;
                    case "JavaScript":
                        result = "@azure/identity";
                        break;
                    case "Python":
                        result = "azure-identity";
                        break;
                }
                return result;
            }
        }

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

        public static LanguageService GetLanguageService(string language, IEnumerable<LanguageService> languageServices)
        {
            return languageServices.FirstOrDefault(service => service.Name == language);
        } 
    }
}
