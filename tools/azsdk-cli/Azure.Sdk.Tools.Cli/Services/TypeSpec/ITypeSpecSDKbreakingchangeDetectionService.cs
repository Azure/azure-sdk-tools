using Azure.Sdk.Tools.Cli.Models.Responses;

namespace Azure.Sdk.Tools.Cli.Services.TypeSpec
{
    public interface ITypeSpecSDKbreakingchangeDetectionService
    {
        /// <summary>
        /// Detects breaking changes in a TypeSpec SDK project compared to a specified baseline (e.g., previous version, main branch, etc.)
        /// </summary>
        /// <param name="typespecChanges">The TypeSpec changes to analyze for breaking changes</param>
        /// <param name="referenceContent">Optional content of a reference document that provides context for the breaking change detection.</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Result of the breaking change detection including whether breaking changes were found and details about them</returns>
        Task<SDKBreakingChangeDetectionResponse> DetectBreakingChangesAsync(
            string typespecChanges,
            string? referenceContent = null,
            CancellationToken ct = default);
    }
}
