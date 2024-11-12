import logging
import inspect
import astroid

from ._base_node import NodeEntityBase
from .._generated.treestyle.parser.models import ReviewToken as Token, TokenKind

class EnumNode(NodeEntityBase):
    """Enum node represents any Enum value
    """

    def __init__(self, *, name, namespace, parent_node, obj):
        super().__init__(namespace, parent_node, obj)
        self.name = name
        self.value = obj.value
        self.namespace_id = self.generate_id()

    def generate_tokens(self, review_lines):
        """Generates token for the node and it's children recursively and add it to apiview
        :param ApiView: apiview
        """
        tokens = []
        tokens.append(Token(kind=TokenKind.TEXT, value=self.name))
        tokens.append(Token(kind=TokenKind.PUNCTUATION, value="="))
        if isinstance(self.value, str):
            tokens.append(Token(kind=TokenKind.STRING_LITERAL, value=self.value))
        else:
            tokens.append(Token(kind=TokenKind.LITERAL, value=str(self.value)))
        for err in self.pylint_errors:
            err.generate_tokens(review_lines, self.namespace_id)
        line = review_lines.create_review_line(line_id=self.namespace_id, tokens=tokens)
        review_lines.append(line)
