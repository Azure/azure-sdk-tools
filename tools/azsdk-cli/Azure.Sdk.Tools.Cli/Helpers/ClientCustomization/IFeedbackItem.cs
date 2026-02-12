// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Helpers.ClientCustomization;

/// <summary>
/// Represents a source of feedback for SDK customization (APIView comments, build errors, etc.)
/// </summary>
public interface IFeedbackItem
{
    /// <summary>
    /// Preprocesses the input and returns a standardized feedback context
    /// </summary>
    Task<FeedbackContext> PreprocessAsync(CancellationToken ct = default);
}
