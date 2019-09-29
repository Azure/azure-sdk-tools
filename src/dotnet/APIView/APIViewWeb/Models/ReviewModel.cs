// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using Newtonsoft.Json;

namespace APIViewWeb
{
    public class ReviewModel
    {
        private bool _runAnalysis;

        [JsonProperty("id")]
        public string ReviewId { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get; set; }
        public string Author { get; set; }
        public DateTime CreationDate { get; set; }
        public ReviewCodeFileModel[] Files { get; set; }

        [JsonIgnore]
        public bool UpdateAvailable { get; set; }

        public bool RunAnalysis
        {
#pragma warning disable 618
            get => _runAnalysis || Files?.Any(f => f.RunAnalysis) == true;
#pragma warning restore 618
            set => _runAnalysis = value;
        }
    }
}