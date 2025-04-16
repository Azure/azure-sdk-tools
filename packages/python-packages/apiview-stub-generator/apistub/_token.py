from ._token_kind import TokenKind


class Token:
    """Entity class to hold individual token information"""

    def __init__(self, value="", kind=TokenKind.Text):
        self.kind = kind
        self.definition_id = None
        self.cross_language_definition_id = None
        self.navigate_to_id = None
        self.value = value

    def render(self):
        rendered_kinds = [
            TokenKind.Text,
            TokenKind.Newline,
            TokenKind.Whitespace,
            TokenKind.Keyword,
            TokenKind.TypeName,
            TokenKind.MemberName,
            TokenKind.StringLiteral,
            TokenKind.Literal,
            TokenKind.Comment,
            TokenKind.Punctuation,
        ]
        if self.kind == TokenKind.Newline:
            return "\n"
        elif self.kind in rendered_kinds:
            return self.value
        else:
            return ""
