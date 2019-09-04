// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using APIView;

namespace ApiView
{
    public class CodeFile
    {
        public const int CurrentVersion = 6;

        public int Version { get; set; }

        public CodeFileToken[] Tokens { get; set; } = Array.Empty<CodeFileToken>();

        public List<NavigationItem> Navigation { get; set; } = new List<NavigationItem>();

        public override string ToString()
        {
            return new CodeFileRenderer().Render(this).ToString();
        }
    }
}