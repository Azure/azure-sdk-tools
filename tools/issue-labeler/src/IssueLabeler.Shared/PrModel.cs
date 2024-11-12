// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
