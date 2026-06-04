// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;

namespace Azure.Sdk.Tools.Mock.Handlers.APIView;

/// <summary>
/// Mock handler for azsdk_apiview_get_comments. Returns a small fixed comment payload so
/// callers can exercise the "consume APIView feedback" path deterministically.
/// </summary>
public class ApiViewGetCommentsHandler : IMockToolHandler
{
    public string ToolName => "azsdk_apiview_get_comments";

    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new APIViewResponse
    {
        Message = "Retrieved APIView comments",
        Language = arguments?.GetValueOrDefault("language")?.ToString() ?? ".NET",
        PackageName = arguments?.GetValueOrDefault("packageName")?.ToString() ?? "Azure.Template.Contoso",
        Result = new[]
        {
            new
            {
                id = "comment-1",
                line = 42,
                text = "Consider renaming this property for clarity.",
                author = "reviewer@microsoft.com",
                resolved = false
            }
        }
    };
}

/// <summary>
/// Mock handler for azsdk_apiview_get_review_url. Returns a deterministic review URL for the
/// requested package + language.
/// </summary>
public class ApiViewGetReviewUrlHandler : IMockToolHandler
{
    public string ToolName => "azsdk_apiview_get_review_url";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var language = arguments?.GetValueOrDefault("language")?.ToString() ?? "dotnet";
        var package = arguments?.GetValueOrDefault("packageName")?.ToString() ?? "Azure.Template.Contoso";
        return new APIViewResponse
        {
            Message = "APIView URL resolved",
            Language = language,
            PackageName = package,
            Result = $"https://apiview.dev/Assemblies/Review/mock-{language}-{package}".ToLowerInvariant()
        };
    }
}

/// <summary>
/// Mock handler for azsdk_apiview_request_copilot_review. Returns a deterministic job ID
/// callers can poll with azsdk_apiview_get_copilot_review.
/// </summary>
public class ApiViewRequestCopilotReviewHandler : IMockToolHandler
{
    public string ToolName => "azsdk_apiview_request_copilot_review";

    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new APIViewResponse
    {
        Message = "Copilot review submitted",
        Language = arguments?.GetValueOrDefault("language")?.ToString() ?? ".NET",
        PackageName = arguments?.GetValueOrDefault("packageName")?.ToString() ?? "Azure.Template.Contoso",
        Result = "mock-copilot-job-00000001"
    };
}

/// <summary>
/// Mock handler for azsdk_apiview_get_copilot_review. Returns a "completed" review with a
/// single sample comment.
/// </summary>
public class ApiViewGetCopilotReviewHandler : IMockToolHandler
{
    public string ToolName => "azsdk_apiview_get_copilot_review";

    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new APIViewResponse
    {
        Message = "Copilot review complete",
        Result = new
        {
            jobId = arguments?.GetValueOrDefault("jobId")?.ToString() ?? "mock-copilot-job-00000001",
            status = "Completed",
            comments = new[]
            {
                new { line = 24, severity = "info", text = "Mock Copilot suggestion: tighten the parameter type." }
            }
        }
    };
}
