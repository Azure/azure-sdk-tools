// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Newtonsoft.Json;

namespace APIViewWeb
{
    public class ReviewModel
    {
        [JsonProperty("id")]
        public string ReviewId { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get; set; }
        public string Author { get; set; }
        public DateTime CreationDate { get; set; }
        public ReviewCodeFileModel[] Files { get; set; }
    }
}