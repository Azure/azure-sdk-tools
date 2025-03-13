using Octokit;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Utils
{
    /// <summary>
    /// Class used to deserialize AI Triage output.
    /// Has four states:
    /// - Empty: No labels, no answer, no answer type
    /// - Labels: Labels are provided, no answer, no answer type. 
    /// - Suggestion: Labels are provided, answer is provided, answer type = "suggestion".
    /// - Solution: Labels are provided, answer is provided, answer type = "solution".
    /// </summary>
    public class IssueTriageOutput
    {
        public List<string> Labels { get; set; }
        public string Answer { get; set; }
        public string AnswerType { get; set; }

        public static readonly IssueTriageOutput Empty = new() { Labels = [], Answer = null, AnswerType = null };
    }
}
