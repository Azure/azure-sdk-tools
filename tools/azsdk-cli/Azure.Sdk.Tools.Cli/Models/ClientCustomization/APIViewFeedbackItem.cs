// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Helpers.ClientCustomization;


/// <summary>
/// Feedback input from APIView review comments
/// </summary>
public class APIViewFeedbackItem : IFeedbackItem
{
    private readonly string _apiViewUrl;
    private readonly IAPIViewFeedbackCustomizationsHelpers _helper;
    private readonly ILogger<APIViewFeedbackItem> _logger;

    public APIViewFeedbackItem(
        string apiViewUrl,
        IAPIViewFeedbackCustomizationsHelpers helper,
        ILogger<APIViewFeedbackItem> logger)
    {
        _apiViewUrl = apiViewUrl;
        _helper = helper;
        _logger = logger;
    }

    public async Task<FeedbackContext> PreprocessAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Preprocessing APIView feedback from: {Url}", _apiViewUrl);

        // Get consolidated comments
        var comments = await _helper.GetConsolidatedComments(_apiViewUrl);
        
        // Get metadata
        var metadata = await _helper.GetMetadata(_apiViewUrl);

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

    private static string FormatCommentForPrompt(ConsolidatedComment comment, string itemId)
    {
        var text = $"API Line {comment.LineNo}: {comment.LineId}, Code: {comment.LineText.Trim()}, ReviewComment: {comment.Comment}";
        return $"Id: {itemId}\nText: {text}\nContext: ";
    }
}
