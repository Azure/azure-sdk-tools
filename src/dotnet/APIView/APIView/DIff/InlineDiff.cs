// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace APIView.DIff
{
    public class InlineDiff
    {
        public static InlineDiffLine<TR>[] Compute<T, TR>(T[] before, T[] after, TR[] beforeResults, TR[] afterResults)
        {
            List<InlineDiffLine<TR>> diffs = new List<InlineDiffLine<TR>>();
            int currentLine = 0;

            void CatchUpTo(int line)
            {
                for (; currentLine < line; currentLine++)
                {
                    diffs.Add(new InlineDiffLine<TR>(afterResults[currentLine], DiffLineKind.Unchanged));
                }
            }

            foreach (var hunk in Diff.GetDiff(before, after))
            {
                if (hunk.IsEmpty)
                {
                    continue;
                }

                CatchUpTo(Math.Min(hunk.RemoveStart, hunk.InsertStart));

                foreach (var hu in beforeResults.AsSpan(hunk.RemoveStart, hunk.Removed))
                {
                    diffs.Add(new InlineDiffLine<TR>(hu, DiffLineKind.Removed));
                }

                foreach (var hu in afterResults.AsSpan(hunk.InsertStart, hunk.Inserted))
                {
                    diffs.Add(new InlineDiffLine<TR>(hu, DiffLineKind.Added));
                }

                currentLine = hunk.InsertStart + hunk.Inserted;
            }


            CatchUpTo(after.Length);
            return diffs.ToArray();
        }
    }
}