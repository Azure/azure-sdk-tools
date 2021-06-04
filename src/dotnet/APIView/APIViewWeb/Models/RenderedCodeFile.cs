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

        public CodeLine[] Render(bool showDocumentation, bool skipDiff = false)
        {
            //Always render when documentation is requested to avoid cach thrashing
            if (showDocumentation)
            {
                return CodeFileHtmlRenderer.Normal.Render(CodeFile, showDocumentation: showDocumentation, enableSkipDiff: skipDiff);
            }

            if (_rendered == null)
            {
                _rendered = CodeFileHtmlRenderer.Normal.Render(CodeFile, enableSkipDiff: skipDiff);
            }

            return _rendered;
        }

        public CodeLine[] RenderReadOnly(bool showDocumentation, bool skipDiff = false)
        {
            if (showDocumentation)
            {
                return CodeFileHtmlRenderer.ReadOnly.Render(CodeFile, showDocumentation: showDocumentation, enableSkipDiff: skipDiff);
            }

            if (_renderedReadOnly == null)
            {
                _renderedReadOnly = CodeFileHtmlRenderer.ReadOnly.Render(CodeFile, skipDiff);
            }

            return _renderedReadOnly;
        }

        internal CodeLine[] RenderText(bool showDocumentation, bool skipDiff = false)
        {
            if (showDocumentation)
            {
                return CodeFileRenderer.Instance.Render(CodeFile, showDocumentation: showDocumentation, enableSkipDiff: skipDiff);
            }

            if (_renderedText == null)
            {
                _renderedText = CodeFileRenderer.Instance.Render(CodeFile, enableSkipDiff: skipDiff);
            }

            return _renderedText;
        }
    }
}