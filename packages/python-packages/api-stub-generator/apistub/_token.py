from ._token_kind import TokenKind


class Token:
    """Entity class to hold individual token information
    """

    def __init__(self, value="", kind=TokenKind.Text):
        self.kind = kind
        self.definition_id = None
        self.cross_language_definition_id = None
        self.navigate_to_id = None
        self.value = value
