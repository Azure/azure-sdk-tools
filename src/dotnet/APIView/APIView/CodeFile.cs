// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using APIView;

namespace ApiView
{
    public class CodeFile
    {
        public CodeFileToken[] Tokens { get; set; } = Array.Empty<CodeFileToken>();

        public override string ToString()
        {
            return new CodeFileRenderer().Render(this).ToString();
        }
    }
}