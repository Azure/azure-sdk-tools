// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using ApiView;
using APIView;
using APIView.DIff;
using Newtonsoft.Json.Serialization;

namespace APIViewWeb.Models
{
    public readonly struct CodeLineModel
    {
        public CodeLineModel(DiffLineKind kind, CodeLine codeLine, CommentThreadModel commentThread,
            CodeDiagnostic[] diagnostics, int lineNumber, int[] documentedByLines = null,
            bool isDiffView = false, int? diffSectionId = null, int? otherLineSectionKey = null, HashSet<int> headingsOfSectionsWithDiff = null, bool isSubHeadingWithDiffInSection = false)
        {
            CodeLine = codeLine;
            CommentThread = commentThread;
            Diagnostics = diagnostics;
            Kind = kind;
            LineNumber = lineNumber;
            DocumentedByLines = documentedByLines;
            IsDiffView = isDiffView;
            DiffSectionId = diffSectionId;
            OtherLineSectionKey = otherLineSectionKey;
            HeadingsOfSectionsWithDiff = headingsOfSectionsWithDiff ?? new HashSet<int>();
            IsSubHeadingWithDiffInSection = isSubHeadingWithDiffInSection;
        }

        public CodeLineModel(CodeLineModel codeLineModel, DiffLineKind kind = DiffLineKind.Unchanged, CodeLine codeLine = default(CodeLine),
            CommentThreadModel commentThread = default(CommentThreadModel), CodeDiagnostic[] diagnostics = null,
            int lineNumber = default(int), int[] documentedByLines = null, bool isDiffView = false, int? diffSectionId = null,
            int? otherLineSectionKey = null, HashSet<int> headingsOfSectionsWithDiff = null, bool isSubHeadingWithDiffInSection = false)
        {
            CodeLine = (codeLine.Equals(default(CodeLine))) ? codeLineModel.CodeLine : codeLine;
            CommentThread = commentThread ?? codeLineModel.CommentThread;
            Diagnostics = diagnostics ?? codeLineModel.Diagnostics;
            Kind = (kind == DiffLineKind.Unchanged) ? codeLineModel.Kind : kind;
            LineNumber = (lineNumber == default(int)) ? codeLineModel.LineNumber : lineNumber;
            DocumentedByLines = documentedByLines ?? codeLineModel.DocumentedByLines;
            IsDiffView = (!isDiffView) ? codeLineModel.IsDiffView : isDiffView;
            DiffSectionId = diffSectionId ?? codeLineModel.DiffSectionId;
            OtherLineSectionKey = otherLineSectionKey ?? codeLineModel.OtherLineSectionKey;
            HeadingsOfSectionsWithDiff = headingsOfSectionsWithDiff ?? codeLineModel.HeadingsOfSectionsWithDiff;
            IsSubHeadingWithDiffInSection = (!isSubHeadingWithDiffInSection) ? codeLineModel.IsSubHeadingWithDiffInSection : isSubHeadingWithDiffInSection;
        }

        public CodeLine CodeLine { get; }
        public CodeDiagnostic[] Diagnostics { get; }
        public CommentThreadModel CommentThread { get; }
        public DiffLineKind Kind { get; }
        public int LineNumber { get; }
        public int[] DocumentedByLines { get; }
        public bool IsDiffView { get; }
        public int? DiffSectionId { get; }
        public int? OtherLineSectionKey { get; }
        public HashSet<int> HeadingsOfSectionsWithDiff { get; }
        public bool IsSubHeadingWithDiffInSection { get; }
    }
}
