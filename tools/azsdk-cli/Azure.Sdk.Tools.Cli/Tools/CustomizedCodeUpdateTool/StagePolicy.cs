// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Tools;

internal static class StagePolicy
{
    public static UpdateStage RequiredPrereq(TspStageSelection selection) => selection switch
    {
        TspStageSelection.Regenerate => UpdateStage.Initialized,
        TspStageSelection.Diff => UpdateStage.Regenerated,
        TspStageSelection.Apply => UpdateStage.PatchesProposed,
        TspStageSelection.All => UpdateStage.Initialized,
        _ => UpdateStage.Initialized
    };

    public static bool CanRun(UpdateSessionState s, TspStageSelection selection, out string? nextHint)
    {
        nextHint = null;
        switch (selection)
        {
            case TspStageSelection.Diff when s.LastStage < UpdateStage.Regenerated:
                nextHint = ToStageKeyword(TspStageSelection.Regenerate);
                return false;
            case TspStageSelection.Apply when s.LastStage < UpdateStage.PatchesProposed:
                nextHint = ToStageKeyword(TspStageSelection.Diff);
                return false;
            default:
                return true;
        }
    }

    public static bool ShouldRun(UpdateSessionState s, TspStageSelection selection, bool resume, bool runAll)
    {
        if (runAll)
        {
            return true;
        }
        return selection switch
        {
            TspStageSelection.Regenerate => s.LastStage < UpdateStage.Regenerated || !resume,
            TspStageSelection.Diff => s.LastStage < UpdateStage.Diffed,
            TspStageSelection.Apply => s.LastStage < UpdateStage.Applied,
            TspStageSelection.All => true,
            _ => true
        };
    }

    public static string? NextHintAfter(UpdateSessionState s, bool runAll, bool needsFinalize)
    {
        if (needsFinalize)
        {
            return ToStageKeyword(TspStageSelection.Apply);
        }
        if (s.LastStage == UpdateStage.Applied)
        {
            return "validate";
        }
        return null;
    }

    public static string ToStageKeyword(TspStageSelection stage)
    {
        return stage switch
        {
            TspStageSelection.Regenerate => "regenerate",
            TspStageSelection.Diff => "diff",
            TspStageSelection.Apply => "apply",
            TspStageSelection.All => "all",
            _ => string.Empty
        };
    }

    public static void EnsurePrereqOrThrow(UpdateSessionState s, UpdateStage requiredPriorStage, string currentCommand, Func<UpdateSessionState, string?> suggestNext)
    {
        if (s.LastStage < requiredPriorStage)
        {
            var needed = requiredPriorStage.ToString();
            var suggestion = suggestNext(s) ?? "(run previous stage)";
            throw new StageOrderException($"Cannot run '{currentCommand}' before completing stage '{needed}'.", "InvalidStageOrder", suggestion);
        }
    }
}

internal sealed class StageOrderException : Exception
{
    public string Code { get; }
    public string SuggestedCommand { get; }

    public StageOrderException(string message, string code, string suggestedCommand) : base(message)
    {
        Code = code;
        SuggestedCommand = suggestedCommand;
    }
}
