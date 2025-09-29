// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.ClientUpdate;

/// <summary>
/// Service interface for LLM-based unified analysis of customization impact and patch generation.
/// </summary>
public interface IClientUpdateLlmService
{
    /// <summary>
    /// Unified method that analyzes customization impacts and generates patch proposals in a single LLM operation.
    /// This is the primary method that provides both analysis and fixes together efficiently.
    /// </summary>
    /// <param name="customizationContent">The customization file content to analyze</param>
    /// <param name="fileName">Name of the customization file</param>
    /// <param name="structuredChanges">Structured context of API changes</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Tuple containing both impacts and patch proposals</returns>
    Task<(List<CustomizationImpact> impacts, List<PatchProposal> patches)> AnalyzeAndProposePatchesAsync(
        string customizationContent, 
        string fileName, 
        StructuredApiChangeContext structuredChanges, 
        CancellationToken ct);



    /// <summary>
    /// Builds comprehensive prompt for unified LLM analysis and patch generation.
    /// </summary>
    /// <param name="customizationContent">The customization code content</param>
    /// <param name="fileName">Name of the customization file</param>
    /// <param name="structuredChanges">Structured API changes context</param>
    /// <returns>Formatted prompt for unified LLM analysis</returns>
    string BuildDependencyChainAnalysisPrompt(
        string customizationContent, 
        string fileName, 
        StructuredApiChangeContext structuredChanges);

    /// <summary>
    /// Parses combined LLM response containing both impact analysis and patch proposals.
    /// This handles the unified response format from the single LLM call.
    /// </summary>
    /// <param name="llmResponse">Raw LLM response containing both impacts and patches</param>
    /// <param name="fileName">Name of the file being analyzed</param>
    /// <param name="structuredChanges">Structured context of API changes</param>
    /// <returns>Tuple of impacts and patches</returns>
    (List<CustomizationImpact> impacts, List<PatchProposal> patches) ParseCombinedLlmResponse(
        string llmResponse,
        string fileName,
        StructuredApiChangeContext structuredChanges);
}
