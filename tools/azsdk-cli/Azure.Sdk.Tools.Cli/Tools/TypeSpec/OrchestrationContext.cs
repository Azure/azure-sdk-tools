// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Tools.TypeSpec;

/// <summary>
/// Tracks iteration state for the customization orchestration loop.
/// Used to build context for the classifier and detect stalls.
/// </summary>
public class OrchestrationContext
{
    /// <summary>
    /// The original feedback or request to address.
    /// </summary>
    public string Request { get; }

    /// <summary>
    /// The target programming language (python, java, csharp, etc.).
    /// </summary>
    public string? Language { get; }

    /// <summary>
    /// Current iteration number (1-based).
    /// </summary>
    public int Iteration { get; set; } = 1;

    /// <summary>
    /// TypeSpec changes applied in each iteration.
    /// </summary>
    public List<string> PhaseAChanges { get; } = new();

    /// <summary>
    /// Build errors encountered in each iteration.
    /// </summary>
    public List<string> BuildErrors { get; } = new();

    /// <summary>
    /// Phase B (SDK customization) results.
    /// </summary>
    public List<string> PhaseBResults { get; } = new();

    public OrchestrationContext(string request, string? language)
    {
        Request = request;
        Language = language;
    }

    /// <summary>
    /// Builds the context string for the classifier to analyze.
    /// </summary>
    public string ToClassifierInput()
    {
        var sections = new List<string>
        {
            $"--- Iteration {Iteration} ---",
            Request
        };

        if (PhaseAChanges.Count > 0)
        {
            sections.Add("--- TypeSpec Changes Applied ---");
            sections.AddRange(PhaseAChanges);
        }

        if (BuildErrors.Count > 0)
        {
            sections.Add("--- Build Result ---");
            sections.Add(BuildErrors[^1]); // Most recent error
        }

        if (PhaseBResults.Count > 0)
        {
            sections.Add("--- Code Changes Applied ---");
            sections.Add(PhaseBResults[^1]);
        }

        return string.Join("\n", sections);
    }

    /// <summary>
    /// Detects if the orchestration is stalled (same error appearing twice).
    /// </summary>
    public bool IsStalled()
    {
        if (BuildErrors.Count < 2)
        {
            return false;
        }

        return BuildErrors[^1] == BuildErrors[^2];
    }

    /// <summary>
    /// Records a Phase A change.
    /// </summary>
    public void AddPhaseAChange(string change)
    {
        PhaseAChanges.Add(change);
    }

    /// <summary>
    /// Records a build error.
    /// </summary>
    public void AddBuildError(string error)
    {
        BuildErrors.Add(error);
    }

    /// <summary>
    /// Records a Phase B result.
    /// </summary>
    public void AddPhaseBResult(string result)
    {
        PhaseBResults.Add(result);
    }

    /// <summary>
    /// Records a successful build.
    /// </summary>
    public void AddBuildSuccess()
    {
        BuildErrors.Add("Build succeeded.");
    }
}
