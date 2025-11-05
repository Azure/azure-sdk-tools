using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Factory for creating standardized command responses.
/// </summary>
public interface IResponseFactory
{
    /// <summary>
    /// Creates a package failure response with the specified error message and package info.
    /// </summary>
    /// <param name="message">The error message to include in the response.</param>
    /// <param name="packageInfo">Optional package information to include in the response.</param>
    /// <param name="nextSteps">Optional next steps to include in the response.</param>
    /// <returns>A PackageOperationResponse indicating failure.</returns>
    PackageOperationResponse CreateFailureResponse(string message, PackageInfo? packageInfo = null, string[]? nextSteps = null);

    /// <summary>
    /// Creates a success response with the specified message.
    /// Only works with PackageOperationResponse.
    /// </summary>
    /// <param name="message">The success message to include in the response.</param>
    /// <param name="packageInfo">Optional package information to include in the response.</param>
    /// <param name="nextSteps">Optional next steps to include in the response.</param>
    /// <returns>A PackageOperationResponse indicating success.</returns>
    PackageOperationResponse CreateSuccessResponse(string message, PackageInfo? packageInfo = null, string[]? nextSteps = null);
}
