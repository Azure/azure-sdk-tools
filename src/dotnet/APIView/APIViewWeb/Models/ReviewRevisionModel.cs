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
        private string _name;

        [JsonProperty("id")]
        public string RevisionId { get; set; } = IdHelper.GenerateId();

        public List<ReviewCodeFileModel> Files { get; set; } = new List<ReviewCodeFileModel>();

        public DateTime CreationDate { get; set; } = DateTime.Now;

        public string Name
        {
            get => _name ?? Files.FirstOrDefault()?.Name;
            set => _name = value;
        }

        [JsonIgnore]
        public ReviewCodeFileModel SingleFile => Files.Single();
    }
}