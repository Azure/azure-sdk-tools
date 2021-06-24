// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace APIViewWeb
{
    public class ReviewRevisionModel
    {
        private string _name;

        private string _author;

        private static readonly Regex s_oldRevisionStyle = new Regex("rev \\d+ -");

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

        [JsonIgnore]
        public ReviewModel Review { get; set; }

        public string Author
        {
            get => _author ?? Review.Author;
            set => _author = value;
        }

        [JsonIgnore]
        public string DisplayName
        {
            get
            {
                string name;
                if (s_oldRevisionStyle.IsMatch(Name))
                {
                    // old model where revision number was stored directly on Name
                    name = Name.Substring(Name.IndexOf('-') + 1);
                }
                else
                {
                    // New model where revision number is calculated on demand. This makes
                    // the feature to allow for editing revision names cleaner.
                    name = Name;
                }
                return Label != null ?
                    $"rev {RevisionNumber} - {Label} - {name}" :
                    $"rev {RevisionNumber} - {name}";
            }
        }

        public string Label { get; set; }

        public int RevisionNumber => Review.Revisions.IndexOf(this);

        public HashSet<string> Approvers { get; set; } = new HashSet<string>();

        public bool IsApproved => Approvers.Count() > 0;
    }
}