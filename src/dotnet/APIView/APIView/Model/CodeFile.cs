// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using APIView;

namespace ApiView
{
    public class CodeFile
    {
        private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions()
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public const int CurrentVersion = 10;

        public int Version { get; set; }

        public string Name { get; set; }

        public string Language { get; set; }

        public CodeFileToken[] Tokens { get; set; } = Array.Empty<CodeFileToken>();

        public NavigationItem[] Navigation { get; set; }

        public CodeDiagnostic[] Diagnostics { get; set; }

        public override string ToString()
        {
            return new CodeFileRenderer().Render(this).ToString();
        }

        public static async Task<CodeFile> DeserializeAsync(Stream stream)
        {
            return await JsonSerializer.DeserializeAsync<CodeFile>(
                stream,
                JsonSerializerOptions);
        }

        public async Task SerializeAsync(Stream stream)
        {
            await JsonSerializer.SerializeAsync(
                stream,
                this,
                JsonSerializerOptions);
        }
    }
}