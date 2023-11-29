using System.Collections.Generic;
using System.Linq;

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

        public static LanguageService GetLanguageService(string language, IEnumerable<LanguageService> languageServices)
        { 
            return languageServices.FirstOrDefault(service => service.Name == language);
        }
    }
}
