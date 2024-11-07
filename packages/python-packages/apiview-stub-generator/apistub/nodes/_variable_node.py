import inspect
from ._base_node import NodeEntityBase
from .._generated.treestyle.parser.models import ReviewToken as Token, TokenKind, add_review_line, set_blank_lines, add_type


class VariableNode(NodeEntityBase):
    """Variable node represents class and instance variable defined in a class
    """

    def __init__(self, *, namespace, parent_node, name, type_name, value, is_ivar, dataclass_properties=None):
        super().__init__(namespace, parent_node, type_name)
        self.name = name
        self.type = type_name
        self.is_ivar = is_ivar
        self.namespace_id = "{0}.{1}({2})".format(
            self.parent_node.namespace_id, self.name, self.type
        )
        self.value = value
        self.dataclass_properties = dataclass_properties

    def generate_tokens(self, review_lines):
        """Generates token for the node
        :param ApiView apiview: apiview
        """

        var_keyword = "ivar" if self.is_ivar else "cvar"
        tokens = []
        tokens.append(Token(kind=TokenKind.KEYWORD, value=var_keyword))
        tokens.append(Token(kind=TokenKind.TEXT, value=self.name))
        # Add type
        if self.type:
            tokens.append(Token(kind=TokenKind.PUNCTUATION, value=":"))
            add_type(tokens, self.type)

        if not self.value:
            add_review_line(review_lines=review_lines, line_id=self.namespace_id, tokens=tokens)
            return

        tokens.append(Token(kind=TokenKind.PUNCTUATION, value="=", has_prefix_space=True))
        if not self.dataclass_properties:
            if self.type in ["str", "Optional[str]"]:
                tokens.append(Token(kind=TokenKind.STRING_LITERAL, value=self.value, has_suffix_space=False))
            else:
                tokens.append(Token(kind=TokenKind.LITERAL, value=self.value, has_suffix_space=False))
        else:
            tokens.append(Token(kind=TokenKind.TEXT, value="field"))
            tokens.append(Token(kind=TokenKind.PUNCTUATION, value="("))
            properties = self.dataclass_properties
            for (i, property) in enumerate(properties):
                print('in field', i, property)
                func_id = f"{self.namespace_id}.field("
                property.generate_tokens(review_lines, line_id=func_id)
                if i < len(properties) - 1:
                    tokens.append(Token(kind=TokenKind.PUNCTUATION, value=","))
            tokens.append(Token(kind=TokenKind.PUNCTUATION, value=")"))
        add_review_line(review_lines=review_lines, line_id=self.namespace_id, tokens=tokens)
