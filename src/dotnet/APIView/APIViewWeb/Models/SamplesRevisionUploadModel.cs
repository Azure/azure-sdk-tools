// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace APIViewWeb.Pages.Assemblies
{
    public class SamplesRevisionUploadModel
    {
        [BindProperty]
        public string SampleString { get; set; }

        [BindProperty]
        public string UpdateString { get; set; }

        [BindProperty]
        public string ReviewId { get; set; }

        [BindProperty]
        public IFormFile File { get; set; }

        [BindProperty]
        public bool Deleting { get; set; } = false;

        [BindProperty]
        public bool DeletingAndRedirect { get; set; } = false;

        [BindProperty]
        public bool Updating { get; set; } = false;

        [BindProperty]
        public bool Renaming { get; set; } = false;

        [BindProperty]
        public string SampleId { get; set; }

        [BindProperty]
        public string RevisionTitle { get; set; }
        [BindProperty]
        public string FileId { get; set; }
    }
}
