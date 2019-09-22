// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace APIViewWeb
{
    public class ReviewCodeFileModel
    {
        public string ReviewFileId { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; }
        public bool HasOriginal { get; set; }
        public bool RunAnalysis { get; set; }
    }
}