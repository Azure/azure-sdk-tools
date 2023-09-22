// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.Generic;

namespace APIViewWeb.Models
{
    public class PackageGroupModel
    {

        public string PackageDisplayName { get; set; }

        public List<ReviewDisplayModel> reviews { get; set; } = new ();
    }
}
