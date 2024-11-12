// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace IssueLabeler.Shared
{
    public class LabelSuggestion
    {
        public string ModelConfigName { get; set; }
        public List<ScoredLabel> LabelScores { get; set; }
    }
}