// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class SwiftLanguageService : JsonLanguageService
    {
        public override string Name { get; } = "Swift";

        //Swift doesn't have any parser for now
        //It will upload a json file with name Swift so Swift reviews are listed under that filter type
        public SwiftLanguageService(IConfiguration configuration)
        {

        }
    }
}