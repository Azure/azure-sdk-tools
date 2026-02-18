namespace APIViewWeb.Helpers;

public static class ApiViewConstants
{
    public const string AzureSdkBotName = "azure-sdk";
    
    // Language constants
    public const string TypeSpecLanguage = "TypeSpec";
    public const string SwaggerLanguage = "Swagger";
    
    // Metadata file names
    public const string TypeSpecMetadataFileName = "typespec-metadata.json";

    public static readonly string[] SdkLanguages = { "C#", "Java", "Python", "Go", "JavaScript" };

    public static readonly string[] AllSupportedLanguages = { "TypeSpec", "C#", "Java", "Python", "Go", "JavaScript"};
}
