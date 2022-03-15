from enum import Enum


class TokenKind(Enum):
    Text = 0
    Newline = 1
    Whitespace = 2
    Punctuation = 3
    Keyword = 4
    LineIdMarker = 5
    TypeName = 6
    MemberName = 7
    StringLiteral = 8
    Literal = 9
    Comment = 10
    DocumentRangeStart = 11
    DocumentRangeEnd = 12
    DeprecatedRangeStart = 13
    DeprecatedRangeEnd = 14
    SkipDiffRangeStart = 15
    SkipDiffRangeEnd = 16
