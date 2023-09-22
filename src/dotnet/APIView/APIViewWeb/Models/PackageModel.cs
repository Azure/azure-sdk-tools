// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using CsvHelper.Configuration.Attributes;

namespace APIViewWeb.Models
{
    public class PackageModel
    {
        [Name("Package")]
        public string Name { get; set; }

        [Name("DisplayName")]
        public string DisplayName { get; set; }

        [Name("ServiceName")]
        public string ServiceName { get; set; }

        [Name("New")]
        public bool IsNew { get; set; }

        [Name("GroupId")]
        public string GroupId { get; set; }
    }
}
