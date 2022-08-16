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

        public CodeLine[] Render()
        {
            return CodeFileHtmlRenderer.Normal.Render(CodeFile);
            if (_rendered == null)
            {
                _rendered = CodeFileHtmlRenderer.Normal.Render(CodeFile);
            }

            return _rendered;
        }

        public CodeLine[] RenderReadOnly()
        {
            _renderedReadOnly = CodeFileHtmlRenderer.ReadOnly.Render(CodeFile);
            if (_renderedReadOnly == null)
            {
                _renderedReadOnly = CodeFileHtmlRenderer.ReadOnly.Render(CodeFile);
            }

            return _renderedReadOnly;
        }

        internal CodeLine[] RenderText(bool skipDiff = false)
        {
            if (true || skipDiff)
            {
                return CodeFileRenderer.Instance.Render(CodeFile, enableSkipDiff: skipDiff);
            }

            if (_renderedText == null)
            {
                _renderedText = CodeFileRenderer.Instance.Render(CodeFile);
            }

            return _renderedText;
        }
    }
}
