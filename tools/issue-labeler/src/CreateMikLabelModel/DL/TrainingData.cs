// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CreateMikLabelModel.Models;
using Octokit;

namespace CreateMikLabelModel
{
    internal static class TrainingData
    {
        public static void WriteTrainingItems(
            IEnumerable<TrainingDataItem> trainingItems,
            StreamWriter outputWriter)
        {
            var ordered = trainingItems
                .OrderBy(x => x.CreatedAt.UtcDateTime.ToFileTimeUtc())  //-> first by created date
                .ThenBy(x => x.RepositoryName)                          //-> then by repo name
                .ThenBy(x => x.Identifier)                              //-> then by issue number
                .Select(x => x.Data);

            foreach (var item in ordered)
            {
                outputWriter.WriteLine(item);
            }
        }
        public static void WriteHeader(StreamWriter outputWriter)
        {
            outputWriter.WriteLine("CombinedID\tID\tLabel\tTitle\tDescription\tAuthor\tIsPR\tFilePaths");
        }

        public static string CreateTrainingData(
            string labelName,
            string repositoryName,
            Issue source) => GetCompressedLine(null, labelName, source.User.Login, source.Body, source.Title, source.CreatedAt, source.Id, repositoryName, false);

        public static string CreateTrainingData(
            string labelName,
            string repositoryName,
            PullRequestWithFiles source) => GetCompressedLine(source.FilePaths, labelName, source.PullRequest.User.Login, source.PullRequest.Body, source.PullRequest.Title, source.PullRequest.CreatedAt, source.PullRequest.Id, repositoryName, true);

        public static string[] SplitFilePaths(string joinedFilePaths) => joinedFilePaths.Split(';');

        private static string GetCompressedLine(
            IEnumerable<string> filePaths,
            string label,
            string author,
            string body,
            string title,
            DateTimeOffset createdAt,
            long identifier,
            string repositoryName,
            bool isPullRequest)
        {
            var createdAtTicks = createdAt.UtcDateTime.ToFileTimeUtc();

            author ??= "ghost";
            body = (body?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Replace('"', '`');
            title = title.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Replace('"', '`');

            if (isPullRequest)
            {
                var filePathsJoined = string.Join(";", filePaths);
                return $"{createdAtTicks},{repositoryName},{identifier}\t{identifier}\t{label}\t{title}\t{body}\t{author}\t1\t{filePathsJoined}";
            }
            else
            {
                return $"{createdAtTicks},{repositoryName},{identifier}\t{identifier}\t{label}\t{title}\t{body}\t{author}\t0\t";
            }
        }
    }
}