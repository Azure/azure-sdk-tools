// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
namespace APIView
{
    public enum CodeFileTokenKind
    {
        Text = 0,
        Newline = 1,
        Whitespace = 2,
        Punctuation = 3,
        Keyword = 4,
        LineIdMarker = 5,
        TypeName = 6,
        MemberName = 7,
        StringLiteral = 8,
        Literal = 9,
        Comment = 10,
        DocumentRangeStart = 11,
        DocumentRangeEnd = 12,
        DeprecatedRangeStart = 13,
        DeprecatedRangeEnd = 14
    }
}