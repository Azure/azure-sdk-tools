// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace APIViewWeb
{
    public class UsageSampleFileModel
    {
        public string UsageSampleFileId { get; set; } = IdHelper.GenerateId();

        public DateTime CreationDate { get; set; } = DateTime.Now;
    }
}
