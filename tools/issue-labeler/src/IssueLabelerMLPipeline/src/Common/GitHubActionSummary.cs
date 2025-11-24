// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Actions.Core.Summaries;

namespace Actions.Core.Services;

/// <summary>
/// This class provides methods to manage the GitHub action summary.
/// </summary>
public static class GitHubActionSummary
{
    private static List<Action<Summary>> persistentSummaryWrites = [];

    /// <summary>
    /// Add persistent writes to the GitHub action summary, emitting them immediately
    /// and storing them for future rewrites when the summary is updated.
    /// </summary>
    /// <param name="summary">The GitHub action summary.</param>
    /// <param name="writeToSummary">The invocation that results in adding content to the summary, to be replayed whenever the persistent summary is rewritten.</param>
    public static void AddPersistent(this Summary summary, Action<Summary> writeToSummary)
    {
        persistentSummaryWrites.Add(writeToSummary);
        writeToSummary(summary);
    }

    /// <summary>
    /// Writes a status message to the GitHub action summary and emits it immediately, always printing
    /// the status at the top of the summary, with other persistent writes below it.
    /// </summary>
    /// <param name="action">The GitHub action service.</param>
    /// <param name="message">The status message to write.</param>
    /// <returns>The async task.</returns>
    public static async Task WriteStatusAsync(this ICoreService action, string message)
    {
        action.WriteInfo(message);

        await action.Summary.WritePersistentAsync(summary =>
        {
            summary.AddMarkdownHeading("Status", 3);
            summary.AddRaw(message);

            if (persistentSummaryWrites.Any())
            {
                summary.AddMarkdownHeading("Results", 3);
            }
        });
    }

    /// <summary>
    /// Writes the persistent summary to the GitHub action summary, clearing it first.
    /// </summary>
    /// <param name="summary">The GitHub action summary.</param>
    /// <param name="writeStatus">An optional action to write a status message to the summary.</param>
    /// <returns>The async task.</returns>
    public static async Task WritePersistentAsync(this Summary summary, Action<Summary>? writeStatus = null)
    {
        await summary.ClearAsync();

        if (writeStatus is not null)
        {
            writeStatus(summary);
        }

        foreach (var write in persistentSummaryWrites)
        {
            write(summary);
        }

        await summary.WriteAsync();
    }
}
