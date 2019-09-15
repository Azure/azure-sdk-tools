// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace APIView
{
    public class CodeDiagnostic
    {
        public CodeDiagnostic()
        {
        }

        public CodeDiagnostic(string targetId, string text, string helpLinkUri)
        {
            Text = text;
            TargetId = targetId;
            HelpLinkUri = helpLinkUri;
        }

        public string Text { get; set; }

        public string HelpLinkUri { get; set; }

        public string TargetId { get; set; }
    }
}
