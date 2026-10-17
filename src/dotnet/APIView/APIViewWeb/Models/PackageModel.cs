// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Text.Json.Serialization;
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

    /// <summary>
    /// Represents the plane classification of a package
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PackageType
    {
        /// <summary>
        /// Data plane package (client libraries for Azure services)
        /// </summary>
        client = 0,

        /// <summary>
        /// Management plane package (resource management libraries)
        /// </summary>
        mgmt = 1,
    }
}
