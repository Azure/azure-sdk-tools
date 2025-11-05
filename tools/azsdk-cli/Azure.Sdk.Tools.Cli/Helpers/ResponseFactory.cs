using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Factory for creating standardized command responses.
/// </summary>
public class ResponseFactory : IResponseFactory
{
    /// <inheritdoc />
    public PackageOperationResponse CreateFailureResponse(string message, PackageInfo? packageInfo = null, string[]? nextSteps = null)
    {
        return new PackageOperationResponse
        {
            ResponseErrors = [message],
            PackageName = packageInfo?.PackageName ?? string.Empty,
            Language = packageInfo?.Language ?? SdkLanguage.Unknown,
            PackageType = packageInfo?.SdkType ?? SdkType.Unknown,
            Result = "failed",
            NextSteps = nextSteps?.ToList() ?? []
        };
    }

    /// <inheritdoc />
    public PackageOperationResponse CreateSuccessResponse(string message, PackageInfo? packageInfo = null, string[]? nextSteps = null)
    {
        return new PackageOperationResponse
        {
            Result = "succeeded",
            Message = message,
            PackageName = packageInfo?.PackageName ?? string.Empty,
            Language = packageInfo?.Language ?? SdkLanguage.Unknown,
            PackageType = packageInfo?.SdkType ?? SdkType.Unknown,
            NextSteps = nextSteps?.ToList() ?? []
        };
    }
}
