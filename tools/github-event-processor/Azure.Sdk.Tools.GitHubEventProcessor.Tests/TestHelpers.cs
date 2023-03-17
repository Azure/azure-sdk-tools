using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Tests
{
    public static class TestHelpers
    {
        public static string GetTestEventPayload(string eventJsonFile)
        {
            string rawJson = File.ReadAllText(eventJsonFile);
            return rawJson;
        }

        // There are some rules, like the issue_comment rule, ReopenIssue, that require the ClosedBy
        // Date to be "less than 7 days ago" and with the static paylods, this won't quite work. Load
        // the rawJson, 
        public static string SetClosedByDateToYesterday(string rawJson)
        {

            DateTime yesterday = DateTime.UtcNow.AddDays(0 - 1);
            DateTimeOffset yesterdayOffset = new DateTimeOffset(yesterday);

            using var doc = JsonDocument.Parse(rawJson);
            // The actions event payload for a pull_request has a class on the pull request that
            // the OctoKit.PullRequest class does not have. This will be null if the user the user
            // does not have Auto-Merge enabled through the pull request UI and will be non-null if
            // the user enabled it through the UI. An AutoMergeEnabled was added to the root of the
            // PullRequestEventGitHubPayload class, which defaults to false. The actual information
            // in the auto_merge is not necessary for any rules processing other than knowing whether
            // or not it's been set.
            var closedAtProp = doc.RootElement.GetProperty("issue").GetProperty("closed_at");
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            string closedAt = closedAtProp.GetString();
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8604 // Possible null reference argument.
            string newJson = rawJson.Replace(closedAt, yesterdayOffset.ToUniversalTime().ToString(yesterdayOffset.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'")));
#pragma warning restore CS8604 // Possible null reference argument.

            return newJson;
        }

    }
}
