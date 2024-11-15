// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace IssueLabeler.Shared
{
    public interface IDiffHelper
    {
        IEnumerable<string> ExtensionsOf(string[] fileDiffs);
        IEnumerable<string> FilenamesOf(string[] fileDiffs);
        string FlattenWithWhitespace(Dictionary<string, int> folder);
        SegmentedDiff SegmentDiff(string[] fileDiffs);
    }
}