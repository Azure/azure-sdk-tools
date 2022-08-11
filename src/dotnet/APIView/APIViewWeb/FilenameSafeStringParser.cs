namespace APIViewWeb
{
    public class FilenameSafeStringParser
    {
        public static string ParseLanguageName(string languageName)
        {
            switch(languageName.ToLower())
            {
                case "c#":
                    return "csharp";
                case "c++":
                    return "cplusplus";
                default:
                    return languageName.ToLower();
            }
        }
    }
}
