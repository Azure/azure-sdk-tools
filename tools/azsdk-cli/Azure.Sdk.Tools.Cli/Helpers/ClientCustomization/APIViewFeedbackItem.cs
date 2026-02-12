// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.APIView;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Helpers.ClientCustomization;

/// <summary>
/// Feedback input from APIView review comments
/// </summary>
public class APIViewFeedbackItem : IFeedbackItem
{
    private readonly string _apiViewUrl;
    private readonly IAPIViewFeedbackService _feedbackService;
    private readonly ILogger<APIViewFeedbackItem> _logger;

    public APIViewFeedbackItem(
        string apiViewUrl,
        IAPIViewFeedbackService feedbackService,
        ILogger<APIViewFeedbackItem> logger)
    {
        _apiViewUrl = apiViewUrl;
        _feedbackService = feedbackService;
        _logger = logger;
    }

    public async Task<FeedbackContext> PreprocessAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Preprocessing APIView feedback from: {Url}", _apiViewUrl);

        // Extract revisionId from URL
        var (revisionId, _) = ApiViewUrlParser.ExtractIds(_apiViewUrl);
        
        // Get metadata using the revisionId
        var metadata = await _feedbackService.ParseReviewMetadata(revisionId);
        
        // Get consolidated comments
        var comments = await _feedbackService.GetConsolidatedComments(revisionId);

        // Convert to feedback items
        var feedbackItems = comments.Select(c =>
        {
            var text = $"API Line {c.LineNo}: {c.LineId}, Code: {c.LineText.Trim()}, ReviewComment: {c.Comment}";
            var item = new FeedbackItem
            {
                Text = text,
                Context = string.Empty
            };
            item.FormattedPrompt = $"Id: {item.Id}\nText: {text}\nContext: ";
            return item;
        }).ToList();

        _logger.LogInformation("Converted {Count} comments to feedback items", feedbackItems.Count);

        return new FeedbackContext
        {
            FormattedFeedback = string.Join("\n\n", feedbackItems.Select(f => f.FormattedPrompt)),
            FeedbackItems = feedbackItems,
            Language = metadata.Language,
            PackageName = metadata.PackageName,
            InputType = "apiview",
            Metadata = new Dictionary<string, string>
            {
                ["APIViewUrl"] = _apiViewUrl
            }
        };
    }
}
