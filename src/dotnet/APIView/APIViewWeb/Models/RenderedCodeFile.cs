// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using ApiView;
using System;

namespace APIViewWeb.Models
{
    public class RenderedCodeFile
    {
        public RenderedCodeFile(CodeFile codeFile)
        {
            CodeFile = codeFile;
        }

        public CodeFile CodeFile { get; }

        public CodeLine[] Render()
        {
            //Always render to avoid cach thrashing
            return CodeFileHtmlRenderer.Normal.Render(CodeFile);
        }

        public CodeLine[] RenderReadOnly()
        {
            return CodeFileHtmlRenderer.ReadOnly.Render(CodeFile);
        }

        internal CodeLine[] RenderText(bool skipDiff = false)
        {
            return CodeFileRenderer.Instance.Render(CodeFile, enableSkipDiff: skipDiff);
        }
    }
}
