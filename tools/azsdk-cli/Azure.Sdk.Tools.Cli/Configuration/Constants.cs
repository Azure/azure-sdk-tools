namespace Azure.Sdk.Tools.Cli.Configuration;

public static class Constants
{
    public const string AZURE_DEVOPS_TOKEN_SCOPE = "499b84ac-1321-427f-aa17-267ca6975798/.default";
    public const string MICROSOFT_CORP_TENANT = "72f988bf-86f1-41af-91ab-2d7cd011db47";

    public const string AZURE_SDK_DEVOPS_BASE_URL = "https://dev.azure.com/azure-sdk";
    public const string AZURE_SDK_DEVOPS_PUBLIC_PROJECT = "public";
    public const string AZURE_SDK_DEVOPS_INTERNAL_PROJECT = "internal";
    public const string AZURE_SDK_DEVOPS_RELEASE_PROJECT = "release";

    public static readonly string ENG_COMMON_PATH = Path.Join("eng", "common");
    public static readonly string ENG_COMMON_SCRIPTS_PATH = Path.Join("eng", "common", "scripts");
    public const string AZURE_OWNER_PATH = "Azure";
    public const string AZURE_SDK_TOOLS_PATH = "azure-sdk-tools";
    public const string AZURE_COMMON_LABELS_PATH = "tools/github/data/common-labels.csv";
    public const string AZURE_CODEOWNERS_PATH = ".github/CODEOWNERS";

    public const string TOOLS_ACTIVITY_SOURCE = "azsdk.tools";
}
