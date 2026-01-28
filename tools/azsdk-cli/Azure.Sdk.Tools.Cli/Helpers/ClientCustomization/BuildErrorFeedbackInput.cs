// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Helpers.ClientCustomization;

/// <summary>
/// Feedback input from build error logs
/// </summary>
public class BuildErrorFeedbackInput : IFeedbackInput
{
    private readonly string _buildLogText;
    private readonly ILogger<BuildErrorFeedbackInput> _logger;
    private readonly string? _language;
    private readonly string? _packagePath;

    public BuildErrorFeedbackInput(
        string buildLogText,
        ILogger<BuildErrorFeedbackInput> logger,
        string? language = null,
        string? packagePath = null)
    {
        _buildLogText = buildLogText;
        _logger = logger;
        _language = language;
        _packagePath = packagePath;
    }

    public Task<FeedbackContext> PreprocessAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Processing build errors");

        // Build errors are treated as a single feedback item for classification
        var feedbackItem = new FeedbackItem
        {
            Id = "BuildLog",
            Context = "Build errors",
            Comment = _buildLogText,
            FormattedForPrompt = _buildLogText  // Pass through as-is
        };

        var context = new FeedbackContext
        {
            FormattedFeedback = _buildLogText,  // Pass through as-is
            FeedbackItems = new List<FeedbackItem> { feedbackItem },
            Language = _language,
            InputType = "build-error",
            Metadata = new Dictionary<string, string>
            {
                ["PackagePath"] = _packagePath ?? string.Empty
            }
        };

        return Task.FromResult(context);
    }
}
