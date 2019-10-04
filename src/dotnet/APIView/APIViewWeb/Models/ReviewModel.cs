// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace APIViewWeb
{
    public class ReviewModel
    {
        private bool _runAnalysis;

        [JsonProperty("id")]
        public string ReviewId { get; set; } = IdHelper.GenerateId();

        public string Name { get; set; }
        public string Author { get; set; }
        public DateTime CreationDate { get; set; }
        public List<ReviewRevisionModel> Revisions { get; set; } = new List<ReviewRevisionModel>();

        [Obsolete("Back compat")]
        public List<ReviewCodeFileModel> Files { get; set; } = new List<ReviewCodeFileModel>();

        [JsonIgnore]
        public bool UpdateAvailable { get; set; }

        public bool RunAnalysis
        {
#pragma warning disable 618
            get => _runAnalysis || Revisions.SelectMany(r=>r.Files).Any(f => f.RunAnalysis);
#pragma warning restore 618
            set => _runAnalysis = value;
        }
    }
}