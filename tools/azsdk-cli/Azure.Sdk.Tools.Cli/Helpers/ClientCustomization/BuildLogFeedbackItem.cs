// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Helpers.ClientCustomization;

/// <summary>
/// Feedback input from build error logs
/// </summary>
public class BuildLogFeedbackItem : IFeedbackItem
{
    private readonly string _buildLogText;
    private readonly ILogger<BuildLogFeedbackItem> _logger;

    public BuildLogFeedbackItem(
        string buildLogText,
        ILogger<BuildLogFeedbackItem> logger)
    {
        _buildLogText = buildLogText;
        _logger = logger;
    }

    public Task<FeedbackContext> PreprocessAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Processing build errors");

        // Treat entire build log as a single feedback item
        var feedbackItem = new FeedbackItem
        {
            Text = "Build log",
            Context = _buildLogText,
            Metadata = new Dictionary<string, string> {}
        };
        
        feedbackItem.FormattedPrompt = $"Id: {feedbackItem.Id}\nText: {_buildLogText}\nContext: {feedbackItem.Context}";

        var context = new FeedbackContext
        {
            FormattedFeedback = _buildLogText,
            FeedbackItems = new List<FeedbackItem> { feedbackItem },
            InputType = "build-log",
            Metadata = new Dictionary<string, string>{}
        };

        return Task.FromResult(context);
    }
}
