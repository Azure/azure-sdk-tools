// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/// <summary>
/// Provides utility methods for handling data files.
/// </summary>
public static class DataFileUtils
{
    /// <summary>
    /// Ensures that the directory for the specified output file exists, recursively creating it if necessary.
    /// </summary>
    /// <param name="outputFile">The path of the output file.</param>
    public static void EnsureOutputDirectory(string outputFile)
    {
        string? outputDir = Path.GetDirectoryName(outputFile);

        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }
    }

    /// <summary>
    /// Sanitizes the specified text by replacing certain characters to ensure it is collapsed onto a single line and compatible with tab-separated values.
    /// </summary>
    /// <param name="text">The text to sanitize.</param>
    /// <returns>The sanitized text.</returns>
    public static string SanitizeText(string text)
        => text
        .Replace('\r', ' ')
        .Replace('\n', ' ')
        .Replace('\t', ' ')
        .Replace('"', '`')
        .Trim();

    /// <summary>
    /// Sanitizes an array of strings by joining them into a single space-separated string.
    /// </summary>
    /// <param name="texts">The array of strings to sanitize.</param>
    /// <returns>The sanitized text.</returns>
    public static string SanitizeTextArray(string[] texts)
        => string.Join(" ", texts.Select(SanitizeText));

    /// <summary>
    /// Formats an issue record into a tab-separated string.
    /// </summary>
    /// <param name="label">The label of the issue.</param>
    /// <param name="title">The title of the issue.</param>
    /// <param name="body">The body of the issue.</param>
    /// <returns>The formatted issue record.</returns>
    public static string FormatIssueRecord(string categoryLabel, string serviceLabel, string title, string body)
        => string.Join('\t',
        [
            SanitizeText(categoryLabel),
            SanitizeText(serviceLabel),
            SanitizeText(title),
            SanitizeText(body)
        ]);

    /// <summary>
    /// Formats a pull request record into a tab-separated string.
    /// </summary>
    /// <param name="label">The label of the pull request.</param>
    /// <param name="title">The title of the pull request.</param>
    /// <param name="body">The body of the pull request.</param>
    /// <param name="fileNames">The array of file names associated with the pull request.</param>
    /// <param name="folderNames">The array of folder names associated with the pull request.</param>
    /// <returns>The formatted pull request record.</returns>
    public static string FormatPullRequestRecord(string categoryLabel, string serviceLabel, string title, string body, string[] fileNames, string[] folderNames)
        => string.Join('\t',
        [
            SanitizeText(categoryLabel),
            SanitizeText(serviceLabel),
            SanitizeText(title),
            SanitizeText(body),
            SanitizeTextArray(fileNames),
            SanitizeTextArray(folderNames)
        ]);
}
