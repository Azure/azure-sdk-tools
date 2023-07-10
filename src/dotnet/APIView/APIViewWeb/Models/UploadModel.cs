// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIViewWeb.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace APIViewWeb.Pages.Assemblies
{
    [UploadModelValidation()]
    public class UploadModel
    {
        [BindProperty]
        public bool RunAnalysis { get; set; }

        [BindProperty]
        public IFormFile[] Files { get; set; }

        [BindProperty]
        public string Language { get; set; }

        [BindProperty]
        public string FilePath { get; set; }

        public static string[] SupportedLanguages => LanguageService.SupportedLanguages;
    }
}
