// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Newtonsoft.Json;

namespace APIView
{
    public class CodeDiagnostic
    {
        public CodeDiagnostic()
        {
        }

        public CodeDiagnostic(string diagnosticId, string targetId, string text, string helpLinkUri, CodeDiagnosticLevel level = CodeDiagnosticLevel.Error)
        {
            DiagnosticId = diagnosticId;
            Text = text;
            TargetId = targetId;
            HelpLinkUri = helpLinkUri;
            Level = level;
        }
        [JsonProperty("di")]
        public string DiagnosticId { get; set; }
        [JsonProperty("t")]
        public string Text { get; set; }
        [JsonProperty("hlu")]
        public string HelpLinkUri { get; set; }
        [JsonProperty("ti")]
        public string TargetId { get; set; }
        [JsonProperty("l")]
        public CodeDiagnosticLevel Level { get; set; }
    }
}
