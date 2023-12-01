// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace APIViewWeb
{
    public class APICodeFileModel
    {
        private string _language;

        public string FileId { get; set; } = IdHelper.GenerateId();

        // This is field is more of a display name. It is set to name value returned by parser which has package name and version in following format
        // Package name ( Version )
        public string Name { get; set; }
        public string Language
        {
            get => _language ?? (Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? "Json" : "C#");
            set => _language = value;
        }

        public string VersionString { get; set; }

        public string LanguageVariant { get; set; }

        public bool HasOriginal { get; set; }

        public DateTime CreationDate { get; set; } = DateTime.Now;

        [Obsolete("Back compat don't use directly")]
        public bool RunAnalysis { get; set; }

        // Field is used to store package name returned by parser. This is used to lookup review for a specific package
        public string PackageName { get; set; }

        // This field stores original file name uploaded to create review
        public string FileName { get; set; }
        public string PackageVersion { get; set; }
    }
}
