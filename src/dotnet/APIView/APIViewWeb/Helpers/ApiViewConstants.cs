namespace APIViewWeb.Helpers;

public static class ApiViewConstants
{
    public const string AzureSdkBotName = "azure-sdk";
    
    // Language constants
    public const string TypeSpecLanguage = "TypeSpec";
    public const string SwaggerLanguage = "Swagger";
    
    /// <summary>
    /// Supported SDK languages (excludes TypeSpec)
    /// </summary>
    public static readonly string[] SdkLanguages = { "C#", "Java", "Python", "Go", "JavaScript" };
    
    /// <summary>
    /// All supported languages including TypeSpec and SDK languages
    /// </summary>
    public static readonly string[] AllSupportedLanguages = { TypeSpecLanguage, "C#", "Java", "Python", "Go", "JavaScript" };
}
