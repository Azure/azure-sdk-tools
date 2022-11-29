// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace APIView.DIff
{
    public class InlineDiff
    {
        public static InlineDiffLine<TR>[] Compute<T, TR>(T[] before, T[] after, TR[] beforeResults, TR[] afterResults) where T : IEquatable<T>
        {
            List<InlineDiffLine<TR>> diffs = new List<InlineDiffLine<TR>>();
            int currentLine = 0;
            int beforeIndex = 0;

            void CatchUpTo(int line)
            {
                for (; currentLine < line; currentLine++, beforeIndex++)
                {
                    TR beforeLine = default(TR);
                    if (beforeIndex < beforeResults.Length)
                    {
                        beforeLine = beforeResults[beforeIndex];
                    }
                    diffs.Add(new InlineDiffLine<TR>(afterResults[currentLine], beforeLine, DiffLineKind.Unchanged));
                }
            }

            foreach (var hunk in Diff.GetDiff(before, after))
            {
                if (hunk.IsEmpty)
                {
                    continue;
                }

                CatchUpTo(hunk.InsertStart);

                foreach (var line in beforeResults.AsSpan(hunk.RemoveStart, hunk.Removed))
                {
                    beforeIndex++;
                    diffs.Add(new InlineDiffLine<TR>(line, DiffLineKind.Removed));
                }

                foreach (var line in afterResults.AsSpan(hunk.InsertStart, hunk.Inserted))
                {
                    currentLine++;
                    diffs.Add(new InlineDiffLine<TR>(line, DiffLineKind.Added));
                }
            }

            CatchUpTo(after.Length);
            return diffs.ToArray();
        }
    }
}
