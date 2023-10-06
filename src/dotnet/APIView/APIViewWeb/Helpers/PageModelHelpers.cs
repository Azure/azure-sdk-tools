using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using APIView.DIff;
using ApiView;
using APIView;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using System;

namespace APIViewWeb.Helpers
{
    public static class PageModelHelpers
    {
        public static UserPreferenceModel GetUserPreference(UserPreferenceCache preferenceCache, ClaimsPrincipal User)
        {
            return preferenceCache.GetUserPreferences(User).Result;
        }

        public static string GetHiddenApiClass(UserPreferenceModel userPreference)
        {
            var hiddenApiClass = " hidden-api hidden-api-toggleable";
            if (userPreference.ShowHiddenApis != true)
            {
                hiddenApiClass += " d-none";
            }
            return hiddenApiClass;
        }

        public static CodeLineModel[] CreateLines(CodeDiagnostic[] diagnostics, InlineDiffLine<CodeLine>[] lines,
            ReviewCommentsModel comments, bool showDiffOnly, int reviewDiffContextSize, string diffContextSeparator,
            HashSet<int> headingsOfSectionsWithDiff, bool hideCommentRows = false)
        {
            if (showDiffOnly)
            {
                lines = CreateDiffOnlyLines(lines, reviewDiffContextSize, diffContextSeparator);
                if (lines.Length == 0)
                {
                    return Array.Empty<CodeLineModel>();
                }
            }
            List<int> documentedByLines = new List<int>();
            int lineNumberExcludingDocumentation = 0;
            int diffSectionId = 0;

            return lines.Select(
                (diffLine, index) =>
                {
                    if (diffLine.Line.IsDocumentation)
                    {
                        // documentedByLines must include the index of a line, assuming that documentation lines are counted
                        documentedByLines.Add(++index);
                        return new CodeLineModel(
                            kind: diffLine.Kind,
                            codeLine: diffLine.Line,
                            commentThread: comments.TryGetThreadForLine(diffLine.Line.ElementId, out var thread, hideCommentRows) ?
                                thread :
                                null,
                            diagnostics: diffLine.Kind != DiffLineKind.Removed ?
                                diagnostics.Where(d => d.TargetId == diffLine.Line.ElementId).ToArray() :
                                Array.Empty<CodeDiagnostic>(),
                            lineNumber: lineNumberExcludingDocumentation,
                            documentedByLines: new int[] { },
                            isDiffView: true,
                            diffSectionId: diffLine.Line.SectionKey != null ? ++diffSectionId : null,
                            otherLineSectionKey: diffLine.Kind == DiffLineKind.Unchanged ? diffLine.OtherLine.SectionKey : null,
                            headingsOfSectionsWithDiff: headingsOfSectionsWithDiff,
                            isSubHeadingWithDiffInSection: diffLine.IsHeadingWithDiffInSection
                        );
                    }
                    else
                    {
                        CodeLineModel c = new CodeLineModel(
                             kind: diffLine.Kind,
                             codeLine: diffLine.Line,
                             commentThread: diffLine.Kind != DiffLineKind.Removed &&
                                 comments.TryGetThreadForLine(diffLine.Line.ElementId, out var thread, hideCommentRows) ?
                                     thread :
                                     null,
                             diagnostics: diffLine.Kind != DiffLineKind.Removed ?
                                 diagnostics.Where(d => d.TargetId == diffLine.Line.ElementId).ToArray() :
                                 Array.Empty<CodeDiagnostic>(),
                             lineNumber: diffLine.Line.LineNumber ?? ++lineNumberExcludingDocumentation,
                             documentedByLines: documentedByLines.ToArray(),
                             isDiffView: true,
                             diffSectionId: diffLine.Line.SectionKey != null ? ++diffSectionId : null,
                             otherLineSectionKey: diffLine.Kind == DiffLineKind.Unchanged ? diffLine.OtherLine.SectionKey : null,
                             headingsOfSectionsWithDiff: headingsOfSectionsWithDiff,
                             isSubHeadingWithDiffInSection: diffLine.IsHeadingWithDiffInSection
                         );
                        documentedByLines.Clear();
                        return c;
                    }
                }).ToArray();
        }

        public static CodeLineModel[] CreateLines(CodeDiagnostic[] diagnostics, CodeLine[] lines, ReviewCommentsModel comments, bool hideCommentRows = false)
        {
            List<int> documentedByLines = new List<int>();
            int lineNumberExcludingDocumentation = 0;
            return lines.Select(
                (line, index) =>
                {
                    if (line.IsDocumentation)
                    {
                        // documentedByLines must include the index of a line, assuming that documentation lines are counted
                        documentedByLines.Add(++index);
                        return new CodeLineModel(
                            DiffLineKind.Unchanged,
                            line,
                            comments.TryGetThreadForLine(line.ElementId, out var thread, hideCommentRows) ? thread : null,
                            diagnostics.Where(d => d.TargetId == line.ElementId).ToArray(),
                            lineNumberExcludingDocumentation,
                            new int[] { }
                        );
                    }
                    else
                    {
                        CodeLineModel c = new CodeLineModel(
                            DiffLineKind.Unchanged,
                            line,
                            comments.TryGetThreadForLine(line.ElementId, out var thread, hideCommentRows) ? thread : null,
                            diagnostics.Where(d => d.TargetId == line.ElementId).ToArray(),
                            line.LineNumber ?? ++lineNumberExcludingDocumentation,
                            documentedByLines.ToArray()
                        );
                        documentedByLines.Clear();
                        return c;
                    }
                }).ToArray();
        }

        public static int ComputeActiveConversations(CodeLine[] lines, ReviewCommentsModel comments)
        {
            int activeThreads = 0;
            foreach (CodeLine line in lines)
            {
                if (string.IsNullOrEmpty(line.ElementId))
                {
                    continue;
                }

                // if we have comments for this line and the thread has not been resolved.
                // Add "&& !thread.Comments.First().IsUsageSampleComment()" to exclude sample comments from being counted (This also prevents the popup before approval)
                if (comments.TryGetThreadForLine(line.ElementId, out CommentThreadModel thread) && !thread.IsResolved)
                {
                    activeThreads++;
                }
            }
            return activeThreads;
        }

        private static InlineDiffLine<CodeLine>[] CreateDiffOnlyLines(InlineDiffLine<CodeLine>[] lines, int reviewDiffContextSize, string diffContextSeparator)
        {
            var filteredLines = new List<InlineDiffLine<CodeLine>>();
            int lastAddedLine = -1;
            for (int i = 0; i < lines.Count(); i++)
            {
                if (lines[i].Kind != DiffLineKind.Unchanged)
                {
                    // Find starting index for pre context
                    int preContextIndx = Math.Max(lastAddedLine + 1, i - reviewDiffContextSize);
                    if (preContextIndx < i)
                    {
                        // Add sepearator to show skipping lines. for e.g. .....
                        if (filteredLines.Count > 0)
                        {
                            filteredLines.Add(new InlineDiffLine<CodeLine>(new CodeLine(diffContextSeparator, null, null), DiffLineKind.Unchanged));
                        }

                        while (preContextIndx < i)
                        {
                            filteredLines.Add(lines[preContextIndx]);
                            preContextIndx++;
                        }
                    }
                    //Add changed line
                    filteredLines.Add(lines[i]);
                    lastAddedLine = i;

                    // Add post context
                    int contextStart = i + 1, contextEnd = i + reviewDiffContextSize;
                    while (contextStart <= contextEnd && contextStart < lines.Count() && lines[contextStart].Kind == DiffLineKind.Unchanged)
                    {
                        filteredLines.Add(lines[contextStart]);
                        lastAddedLine = contextStart;
                        contextStart++;
                    }
                }
            }
            return filteredLines.ToArray();
        }
    }
}
