// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace APIViewWeb.Pages.Assemblies
{
    public class UsageSampleUploadModel
    {
        [BindProperty]
        public string sampleString { get; set; }

        [BindProperty]
        public string ReviewId { get; set; }

        [BindProperty]
        public IFormFile File { get; set; }

        [BindProperty]
        public bool Deleting { get; set; } = false;
    }
}
