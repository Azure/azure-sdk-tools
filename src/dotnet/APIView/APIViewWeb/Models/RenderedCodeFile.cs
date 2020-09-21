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
        private bool _showDocumentation;

        public RenderedCodeFile(CodeFile codeFile)
        {
            CodeFile = codeFile;
        }

        public CodeFile CodeFile { get; }

        public CodeLine[] Render(bool showDocumentation)
        {
            if (_rendered == null || (_showDocumentation != showDocumentation))
            {
                this._showDocumentation = showDocumentation;
                _rendered = CodeFileHtmlRenderer.Normal.Render(CodeFile, showDocumentation);
            }

            return _rendered;
        }

        public CodeLine[] RenderReadOnly(bool showDocumentation)
        {
            if (_renderedReadOnly == null || (_showDocumentation != showDocumentation))
            {
                this._showDocumentation = showDocumentation;
                _renderedReadOnly = CodeFileHtmlRenderer.ReadOnly.Render(CodeFile, showDocumentation);
            }

            return _renderedReadOnly;
        }

        internal CodeLine[] RenderText(bool showDocumentation)
        {
            if (_renderedText == null || (_showDocumentation != showDocumentation))
            {
                this._showDocumentation = showDocumentation;
                _renderedText = CodeFileRenderer.Instance.Render(CodeFile);
            }

            return _renderedText;
        }
    }
}