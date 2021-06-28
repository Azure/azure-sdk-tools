// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace APIViewWeb
{
    public class ProtocolLanguageService : JsonLanguageService
    {
        public override string Name { get; } = "Protocol";

        public override bool IsSupportedFile(string name)
        {
            // Skip initial processing so this service won't be selected for LLC when json is uploaded
            // This is only a temporary solution for POC and will be remvoed once autorest yaml is uploaded instead of json for LLC
            return false;
        }
    }
}