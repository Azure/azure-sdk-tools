// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.APIView;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Helpers.ClientCustomization;

/// <summary>
/// Feedback source from APIView review comments
/// </summary>
public class APIViewFeedbackSource
{
    private readonly string _apiViewUrl;
    private readonly IAPIViewFeedbackService _feedbackService;
    private readonly ILogger<APIViewFeedbackSource> _logger;

    public APIViewFeedbackSource(
        string apiViewUrl,
        IAPIViewFeedbackService feedbackService,
        ILogger<APIViewFeedbackSource> logger)
    {
        _apiViewUrl = apiViewUrl;
        _feedbackService = feedbackService;
        _logger = logger;
    }

    public async Task<FeedbackBatch> CreateBatchAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Preprocessing APIView feedback from: {Url}", _apiViewUrl);

        var (revisionId, _) = APIViewReviewTool.ExtractIdsFromUrl(_apiViewUrl);

        var metadata = await _feedbackService.ParseReviewMetadata(revisionId);

        var comments = await _feedbackService.GetConsolidatedComments(revisionId);

        var feedbackItems = comments.Select(c =>
        {
            var text = $"API Line {c.LineNo}: {c.LineId}, Code: {c.LineText.Trim()}, ReviewComment: {c.Comment}";
            var item = new FeedbackItem
            {
                Text = text,
                Context = string.Empty
            };
            return item;
        }).ToList();

        _logger.LogInformation("Converted {Count} comments to feedback items", feedbackItems.Count);

        return new FeedbackBatch
        {
            Items = feedbackItems,
            Language = metadata.Language
        };
    }
}