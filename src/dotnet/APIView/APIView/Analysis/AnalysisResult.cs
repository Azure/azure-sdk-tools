using System;
using System.Collections.Generic;
using System.Text;

namespace APIView
{
    public struct AnalysisResult
    {
        public AnalysisResult(string targetId, string text, string helpLinkUri = default)
        {
            Text = text;
            TargetId = targetId;
            HelpLinkUri = helpLinkUri;
        }

        public string Text { get; }

        public string HelpLinkUri { get; }

        public string TargetId { get; }
    }
}
