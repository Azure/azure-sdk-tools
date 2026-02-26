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
    private readonly string _revisionId;
    private ReviewMetadata? _reviewMetadata;

    public APIViewFeedbackSource(
        string apiViewUrl,
        IAPIViewFeedbackService feedbackService,
        ILogger<APIViewFeedbackSource> logger)
    {
        _apiViewUrl = apiViewUrl;
        _feedbackService = feedbackService;
        _logger = logger;
        (_revisionId, _) = APIViewReviewTool.ExtractIdsFromUrl(_apiViewUrl);
    }

    /// <summary>
    /// Returns the SDK language detected from APIView review metadata.
    /// </summary>
    public async Task<string?> GetLanguageAsync(CancellationToken ct = default)
    {
        _reviewMetadata ??= await _feedbackService.ParseReviewMetadata(_revisionId);
        return _reviewMetadata.Language;
    }

    public async Task<List<FeedbackItem>> CreateBatchAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Preprocessing APIView feedback from: {Url}", _apiViewUrl);

        _reviewMetadata ??= await _feedbackService.ParseReviewMetadata(_revisionId);

        var comments = await _feedbackService.GetConsolidatedComments(_revisionId);

        var feedbackItems = comments.Select(c =>
        {
            var text = $"API Line {c.LineNo}: {c.LineId}, Code: {c.LineText.Trim()}, ReviewComment: {c.Comment}";
            return new FeedbackItem
            {
                Text = text,
                Context = string.Empty
            };
        }).ToList();

        _logger.LogInformation("Converted {Count} comments to feedback items", feedbackItems.Count);

        return feedbackItems;
    }
}
