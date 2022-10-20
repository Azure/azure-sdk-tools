// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using ApiView;
using APIView;
using APIView.DIff;

namespace APIViewWeb.Models
{
    public readonly struct CodeLineModel
    {
        public CodeLineModel(DiffLineKind kind, CodeLine codeLine, CommentThreadModel commentThread, CodeDiagnostic[] diagnostics, int lineNumber, int[] documentedByLines = null)
        {
            CodeLine = codeLine;
            CommentThread = commentThread;
            Diagnostics = diagnostics;
            Kind = kind;
            LineNumber = lineNumber;
            DocumentedByLines = documentedByLines;
        }

        public CodeLine CodeLine { get; }
        public CodeDiagnostic[] Diagnostics { get; }
        public CommentThreadModel CommentThread { get; }
        public DiffLineKind Kind { get; }
        public int LineNumber { get; }
        public int[] DocumentedByLines { get; }
    }
}
