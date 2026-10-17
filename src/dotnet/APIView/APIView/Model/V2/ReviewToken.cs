// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace APIView.Model.V2
{
    /// <summary>
    /// Token corresponds to each component within a code line. A separate token is required for keyword, punctuation, type name, text etc.
    /// </summary>
    public class ReviewToken
    {
        public ReviewToken() { }
        public ReviewToken(string value, TokenKind kind)
        {
            Value = value;
            Kind = kind;
        }
        public TokenKind Kind { get; set; }
        public string Value { get; set; }

        /// <summary>
        /// NavigationDisplayName property is to add a short name for the token that will be displayed in the navigation object.
        /// </summary>
        public string NavigationDisplayName { get; set; }

        /// <summary>
        /// navigateToId should be set if the underlying token is required to be displayed as HREF to another type within the review.
        /// </summary>
        public string NavigateToId { get; set; }

        /// <summary>
        /// set skipDiff to true if underlying token needs to be ignored from diff calculation. 
        /// For e.g. package metadata or dependency versions are usually not required to be excluded when comparing two revisions to avoid reporting them as API changes
        /// </summary>
        public bool? SkipDiff { get; set; }

        /// <summary>
        /// This is set if API is marked as deprecated
        /// </summary>
        public bool? IsDeprecated { get; set; }

        /// <summary>
        /// Set this to false if there is no suffix space required before next token. For e.g, punctuation right after method name
        /// </summary>
        public bool HasSuffixSpace { get; set; } = true;
        /// <summary>
        /// Set this to true if there is a prefix space required before current token.
        /// </summary>
        public bool HasPrefixSpace { get; set; } = false;

        /// <summary>
        /// Set isDocumentation to true if current token is part of documentation
        /// </summary>
        public bool? IsDocumentation { get; set; }

        /// <summary>
        /// Language specific style css class names
        /// </summary>
        public List<string> RenderClasses { get; set; } = [];

        public static ReviewToken CreateTextToken(string value, string navigateToId = null, bool hasSuffixSpace = true)
        {
            var token = new ReviewToken(value, TokenKind.Text);
            if (!string.IsNullOrEmpty(navigateToId))
            {
                token.NavigateToId = navigateToId;
            }
            token.HasSuffixSpace = hasSuffixSpace;
            return token;
        }

        public static ReviewToken CreateKeywordToken(string value, bool hasSuffixSpace = true)
        {
            var token = new ReviewToken(value, TokenKind.Keyword);
            token.HasSuffixSpace = hasSuffixSpace;
            return token;
        }

        public static ReviewToken CreateKeywordToken(SyntaxKind syntaxKind, bool hasSuffixSpace = true)
        {
            return CreateKeywordToken(SyntaxFacts.GetText(syntaxKind), hasSuffixSpace);
        }

        public static ReviewToken CreateKeywordToken(Accessibility accessibility)
        {
            return CreateKeywordToken(SyntaxFacts.GetText(accessibility));
        }

        public static ReviewToken CreatePunctuationToken(string value, bool hasSuffixSpace = true)
        {
            var token = new ReviewToken(value, TokenKind.Punctuation);
            token.HasSuffixSpace = hasSuffixSpace;
            return token;
        }

        public static ReviewToken CreatePunctuationToken(SyntaxKind syntaxKind, bool hasSuffixSpace = true)
        {
            var token = CreatePunctuationToken(SyntaxFacts.GetText(syntaxKind), hasSuffixSpace);
            return token;
        }

        public static ReviewToken CreateTypeNameToken(string value, bool hasSuffixSpace = true)
        {
            var token = new ReviewToken(value, TokenKind.TypeName);
            token.HasSuffixSpace = hasSuffixSpace;
            return token;
        }

        public static ReviewToken CreateMemberNameToken(string value, bool hasSuffixSpace = true)
        {
            var token = new ReviewToken(value, TokenKind.MemberName);
            token.HasSuffixSpace = hasSuffixSpace;
            return token;
        }

        public static ReviewToken CreateLiteralToken(string value, bool hasSuffixSpace = true)
        {
            var token = new ReviewToken(value, TokenKind.Literal);
            token.HasSuffixSpace = hasSuffixSpace;
            return token;
        }

        public static ReviewToken CreateStringLiteralToken(string value, bool hasSuffixSpace = true)
        {
            var token = new ReviewToken(value, TokenKind.StringLiteral);
            token.HasSuffixSpace = hasSuffixSpace;
            return token;
        }

        public static ReviewToken CreateCommentToken(string value, bool hasSuffixSpace = true)
        {
            var token = new ReviewToken(value, TokenKind.Comment);
            token.HasSuffixSpace = hasSuffixSpace;
            return token;
        }
    }
}
