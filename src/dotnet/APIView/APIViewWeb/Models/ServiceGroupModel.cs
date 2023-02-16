// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.Generic;

namespace APIViewWeb.Models
{
    public class ServiceGroupModel
    {

        public string ServiceName { get; set; }

        public SortedDictionary<string, PackageGroupModel> packages { get; set; } = new();
    }
}
