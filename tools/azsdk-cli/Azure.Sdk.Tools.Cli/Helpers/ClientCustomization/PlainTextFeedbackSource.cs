// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Helpers.ClientCustomization;

/// <summary>
/// Feedback source from plain text (e.g., build logs, error messages, user input)
/// </summary>
public class PlainTextFeedbackSource
{
    private readonly string _plainText;
    private readonly ILogger<PlainTextFeedbackSource> _logger;

    public PlainTextFeedbackSource(
        string plainText,
        ILogger<PlainTextFeedbackSource> logger)
    {
        _plainText = plainText;
        _logger = logger;
    }

    public List<FeedbackItem> CreateBatch()
    {
        _logger.LogInformation("Processing plain text feedback");

        return [new FeedbackItem
        {
            Text = _plainText,
            Context = string.Empty
        }];
    }
}