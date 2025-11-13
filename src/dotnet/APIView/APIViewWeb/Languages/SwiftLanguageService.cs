// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class SwiftLanguageService : JsonLanguageService
    {
        public override string Name { get; } = "Swift";
        public override string VersionString { get; } = "0.3.0";
        public override bool UsesTreeStyleParser { get; } = true;

        //Swift doesn't have any parser for now
        //It will upload a json file with name Swift so Swift reviews are listed under that filter type
        public SwiftLanguageService(IConfiguration configuration)
        {

        }

        public override bool IsSupportedFile(string name)
        {
            // Skip initial processing so this service won't be selected for LLC when json is uploaded
            // This is only a temporary solution for POC and will be removed once autorest yaml is uploaded instead of json for LLC
            return false;
        }

        public override bool CanUpdate(string versionString)
        {
            return false;
        }

        public override bool CanConvert(string versionString)
        {
            return versionString != VersionString;
        }
    }
}
