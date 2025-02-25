// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.ML.Data;

namespace IssueLabeler.Shared.Models
{
    public class PrModel : IssueModel
    {
        [LoadColumn(9)]
        public float FileCount;

        [LoadColumn(10)]
        public string Files;

        [LoadColumn(11)]
        public string Filenames;

        [LoadColumn(12)]
        public string FileExtensions;

        [LoadColumn(13)]
        public string FolderNames;

        [LoadColumn(14)]
        public string Folders;

        [NoColumn]
        public bool ShouldAddDoc { get; set; } = false;
    }
}
