// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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