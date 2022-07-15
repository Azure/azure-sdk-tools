// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using ApiView;
using System;

namespace APIViewWeb.Models
{
    public class RenderedCodeFile
    {
        private CodeLine[] _rendered;
        private CodeLine[] _renderedReadOnly;
        private CodeLine[] _renderedText;

        public RenderedCodeFile(CodeFile codeFile)
        {
            CodeFile = codeFile;
        }

        public CodeFile CodeFile { get; }

        public RenderResult RenderResult { get; private set; }

        public CodeLine[] Render(bool showDocumentation)
        {
            //Always render when documentation is requested to avoid cach thrashing
            if (showDocumentation)
            {
                RenderResult =  CodeFileHtmlRenderer.Normal.Render(CodeFile, showDocumentation: true);
                return RenderResult.CodeLines;
            }

            if (_rendered == null)
            {
                RenderResult = CodeFileHtmlRenderer.Normal.Render(CodeFile);
                _rendered = RenderResult.CodeLines;
            }

            return _rendered;
        }

        public CodeLine[] RenderReadOnly(bool showDocumentation)
        {
            if (showDocumentation)
            {
                RenderResult = CodeFileHtmlRenderer.ReadOnly.Render(CodeFile, showDocumentation: true);
                return RenderResult.CodeLines;
            }

            if (_renderedReadOnly == null)
            {
                RenderResult = CodeFileHtmlRenderer.ReadOnly.Render(CodeFile);
                _renderedReadOnly = RenderResult.CodeLines;
            }

            return _renderedReadOnly;
        }

        internal CodeLine[] RenderText(bool showDocumentation, bool skipDiff = false)
        {
            if (showDocumentation || skipDiff)
            {
                RenderResult = CodeFileRenderer.Instance.Render(CodeFile, showDocumentation: showDocumentation, enableSkipDiff: skipDiff);
                return RenderResult.CodeLines;
            }

            if (_renderedText == null)
            {
                RenderResult = CodeFileRenderer.Instance.Render(CodeFile);
                _renderedText = RenderResult.CodeLines;
            }

            return _renderedText;
        }
    }
}
