// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace IssueLabeler.Shared
{
    public class LabelSuggestion
    {
        public string ModelConfigName { get; set; }
        public List<ScoredLabel> LabelScores { get; set; }
    }
}