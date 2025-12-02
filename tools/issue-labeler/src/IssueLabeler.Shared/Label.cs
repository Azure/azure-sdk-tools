// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.ML.Data;
namespace IssueLabeler.Shared.Models
{
    public class Label
    {
        [LoadColumn(0)]
        [ColumnName("id")]
        public long Id;

        [LoadColumn(1)]
        [ColumnName("node_id")]
        public string NodeId;

        [LoadColumn(2)]
        [ColumnName("url")]
        public string Url;

        [LoadColumn(3)]
        [ColumnName("name")]
        public string Name;

        [LoadColumn(4)]
        [ColumnName("color")]
        public string Color;

        [LoadColumn(5)]
        [ColumnName("default")]
        public bool Flag;

        [LoadColumn(6)]
        [ColumnName("description")]
        public string Description;
    }
}
