from ._base_node import NodeEntityBase, get_qualified_name
from .._generated.treestyle.parser.models import ReviewToken as Token, TokenKind, add_type

class KeyNode(NodeEntityBase):
    """Key node represents a typed key defined in a TypedDict object
    """

    def __init__(self, namespace, parent_node, name, type_data):
        super().__init__(namespace, parent_node, type_data)
        self.type = get_qualified_name(type_data, namespace)
        self.name = f'"{name}"'
        # Generate ID using name found by inspect
        self.namespace_id = self.generate_id()
        self.display_name = f"{self.name}: {self.type}"

    def generate_tokens(self, review_lines):
        """Generates token for the node and it's children recursively and add it to apiview
        :param review_lines: list[ReviewLine]
        """
        tokens = []
        tokens.append(Token(kind=TokenKind.TEXT, value="key", has_suffix_space=False))
        tokens.append(Token(kind=TokenKind.TEXT, value=self.name, has_suffix_space=False))
        tokens.append(Token(kind=TokenKind.PUNCTUATION, value=":"))
        add_type(tokens, self.type)
        line = review_lines.create_review_line(tokens=tokens, line_id=self.namespace_id)
        review_lines.append(line)
