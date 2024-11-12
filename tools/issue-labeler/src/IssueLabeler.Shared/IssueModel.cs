// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.ML.Data;
using Newtonsoft.Json;

namespace IssueLabeler.Shared.Models
{
    public class IssueModel
    {
        [JsonIgnore]
        [LoadColumn(0)]
        public string CombinedID;

        [LoadColumn(1)]
        public float ID;

        [LoadColumn(2)]
        public string Area;

        [LoadColumn(3)]
        public string Title;

        [LoadColumn(4)]
        public string Description;

        [LoadColumn(5)]
        public string Author;

        [LoadColumn(6)]
        public float IsPR;

        [LoadColumn(7)]
        public string UserMentions;

        [LoadColumn(8)]
        public float NumMentions;

        [NoColumn]
        public List<Label> Labels { get; set; }

        [NoColumn]
        public int Number { get; set; }
    }

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
