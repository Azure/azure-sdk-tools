﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace APIViewWeb
{
    public class ReviewCodeFileModel
    {
        private string _language;

        public string ReviewFileId { get; set; } = IdHelper.GenerateId();
        public string Name { get; set; }

        public string Language
        {
            get => _language ?? (Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? "Json" : "C#");
            set => _language = value;
        }

        public string VersionString { get; set; }

        public bool HasOriginal { get; set; }

        public DateTime CreationDate { get; set; } = DateTime.Now;

        [Obsolete("Back compat don't use directly")]
        public bool RunAnalysis { get; set; }

        public string PackageName { get; set; }
    }
}