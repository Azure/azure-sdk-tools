// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIView;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;

namespace ApiView
{
    public class CodeFileTokensBuilder
    {
        public List<CodeFileToken> Tokens { get; } = new List<CodeFileToken>();

        private int _indentation = 0;

        public void IncrementIndent()
        {
            _indentation++;
        }

        public void DecrementIndent()
        {
            _indentation--;
        }

        public void WriteIndent()
        {
            Append(new string(' ', _indentation * 4), CodeFileTokenKind.Whitespace);
        }

        public void Append(CodeFileToken token)
        {
            Tokens.Add(token);
        }

        public void Append(string value, CodeFileTokenKind kind)
        {
            Tokens.Add(new CodeFileToken(value, kind));
        }

        public void Punctuation(SyntaxKind syntaxKind)
        {
            Append(SyntaxFacts.GetText(syntaxKind), CodeFileTokenKind.Punctuation);
        }

        public void Punctuation(string s)
        {
            Append(s, CodeFileTokenKind.Punctuation);
        }

        public void NewLine()
        {
            Append(new CodeFileToken(null, CodeFileTokenKind.Newline));
        }

        public void Keyword(SyntaxKind syntaxKind)
        {
            Keyword(SyntaxFacts.GetText(syntaxKind));
        }

        public void Keyword(string keyword)
        {
            Append(keyword, CodeFileTokenKind.Keyword);
        }

        public void Space()
        {
            Append(" ", CodeFileTokenKind.Whitespace);
        }

        public void Text(string text)
        {
            Append(text, CodeFileTokenKind.Text);
        }

        public void Comment(string text)
        {
            Append(text, CodeFileTokenKind.Comment);
        }
    }
}