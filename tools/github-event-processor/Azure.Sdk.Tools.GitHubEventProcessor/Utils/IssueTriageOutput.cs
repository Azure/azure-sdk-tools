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
    /// - Empty: No labels, no suggestion, no solution. Menaual triage is required.
    /// - Labels: Labels are provided, no suggestion, no solution. 
    /// - Suggestion: Labels are provided, suggestion is provided, no solution.
    /// - Solution: Labels are provided, suggestion isn't provided, solution is provided.
    /// </summary>
    public class IssueTriageOutput
    {
        public List<string> Labels { get; set; }
        public string Suggestion { get; set; }
        public string Solution { get; set; }
    }
}
