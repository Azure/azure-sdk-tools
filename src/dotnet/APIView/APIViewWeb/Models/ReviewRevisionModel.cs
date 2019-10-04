// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace APIViewWeb
{
    public class ReviewRevisionModel
    {
        [JsonProperty("id")]
        public string RevisionId { get; set; } = IdHelper.GenerateId();

        public string DisplayName { get; set; }

        public List<ReviewCodeFileModel> Files { get; set; } = new List<ReviewCodeFileModel>();

        public DateTime CreationDate { get; set; } = DateTime.Now;

        [JsonIgnore]
        public string Name => Files.Single().Name;
    }
}