// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
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

    public Task<FeedbackBatch> PreprocessAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Processing plain text feedback");

        // Treat entire text as a single feedback item
        var feedbackItem = new FeedbackItem
        {
            Text = _plainText,
            Context = string.Empty
        };

        var batch = new FeedbackBatch
        {
            Items = new List<FeedbackItem> { feedbackItem }
        };

        return Task.FromResult(batch);
    }
}
