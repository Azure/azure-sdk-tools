// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Helpers.ClientCustomization;

/// <summary>
/// Feedback input from plain text (e.g., build logs, error messages, user input)
/// </summary>
public class PlainTextFeedbackItem : IFeedbackItem
{
    private readonly string _plainText;
    private readonly ILogger<PlainTextFeedbackItem> _logger;

    public PlainTextFeedbackItem(
        string plainText,
        ILogger<PlainTextFeedbackItem> logger)
    {
        _plainText = plainText;
        _logger = logger;
    }

    public Task<FeedbackContext> PreprocessAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Processing plain text feedback");

        // Treat entire text as a single feedback item
        var feedbackItem = new FeedbackItem
        {
            Text = "Plain text feedback",
            Context = _plainText,
            Metadata = new Dictionary<string, string> {}
        };
        
        feedbackItem.FormattedPrompt = $"Id: {feedbackItem.Id}\nText: {_plainText}\nContext: {feedbackItem.Context}";

        var context = new FeedbackContext
        {
            FormattedFeedback = _plainText,
            FeedbackItems = new List<FeedbackItem> { feedbackItem },
            InputType = "plain-text",
            Metadata = new Dictionary<string, string>{}
        };

        return Task.FromResult(context);
    }
}
