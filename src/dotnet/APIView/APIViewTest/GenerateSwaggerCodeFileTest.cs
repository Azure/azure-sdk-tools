// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using APIView.DIff;
using APIViewWeb;
using Xunit;

namespace APIViewTest
{
    public class GenerateSwaggerCodeFileTest
    {

        public async void generateForFile(string input, string output)
        {
            var fileReadStream = File.OpenRead(input);
            var ls = new SwaggerLanguageService();
            var cf = await ls.GetCodeFileInternalAsync(input, fileReadStream, false);
            var fileWriteStream = File.OpenWrite(output);
            await cf.SerializeAsync(fileWriteStream);
        }

        [Fact]
        public async void  generate()
        {
            generateForFile("cosmos-db-2021-06-15.json", "cosmos-db-2021-06-15-codefile.json");
            generateForFile("cosmos-db-2021-10-15.json", "cosmos-db-2021-10-15-codefile.json");
        }
    }
}
