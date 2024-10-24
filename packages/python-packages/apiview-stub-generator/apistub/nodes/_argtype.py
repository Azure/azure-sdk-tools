import astroid
import inspect

from ._base_node import get_qualified_name
from .._generated.treestyle.parser.models import ReviewToken as Token, TokenKind, add_review_line

# Special default values that should not be treated as string literal
SPECIAL_DEFAULT_VALUES = ["None", "..."]


class ArgType:
    """Represents Argument type
    :param str name: Name of the argument.
    :param str argtype: Type of the argument (e.g. str, int, BlobBlock).
    :param str default: Default value for the argument, If any.
    :param str keyword: The keyword for the arg type.
    :param FunctionNode func_node: The function node this belongs to.
    """
    def __init__(self, name, *, argtype, default, keyword, func_node=None):
        self.argname = name
        if default == inspect.Parameter.empty:
            self.is_required = True
            self.default = None
        elif default is None:
            self.is_required = False
            self.default = "..." if keyword == "keyword" else "None"
        else:
            self.is_required = False
            self.default = default

        if argtype and all([not self.is_required, self.default is None, not keyword in ["ivar", "param"], not argtype.startswith("Optional")]):
            self.argtype = f"Optional[{argtype}]"
        else:
            self.argtype = argtype
        self.function_node = func_node

    def generate_tokens(self, review_lines, function_id, namespace, *, add_line_marker: bool, prefix: str = ""):
        """Generates token for the node and it's children recursively and add it to apiview
        :param ~ReviewLine apiview: The ApiView
        :param str function_id: Module level Unique ID created for function 
        :keyword bool add_line_marker: Flag to indicate whether to include a line ID marker or not.
        :keyword str prefix: Optional prefix for *args and **kwargs.
        """
        # Add arg name
        self.id = function_id
        if add_line_marker:
            self.id = f"{function_id}.param({self.argname})"

        tokens = []
        tokens.append(Token(kind=TokenKind.TEXT, value=f"{prefix}{self.argname}", has_suffix_space=False))
        # add arg type
        if self.argtype:
            tokens.append(Token(kind=TokenKind.PUNCTUATION, value=":"))
            tokens.append(Token(kind=TokenKind.TYPE_NAME, value=self.argtype, has_suffix_space=False))

        add_review_line(review_lines, line_id=self.id, tokens=tokens)

        # add arg default value
        default = self.default
        if default is not None:
            # TODO: add has_prefix_space=True if prefix is not empty
            tokens.append(Token(kind=TokenKind.PUNCTUATION, value="=", has_suffix_space=True))
            if isinstance(default, str) and default not in SPECIAL_DEFAULT_VALUES:
                tokens.append(Token(kind=TokenKind.STRING_LITERAL, value=default, has_suffix_space=False))
            else:
                if isinstance(default, astroid.node_classes.Name):
                    value = default.name
                elif hasattr(default, "as_string"):
                    value = default.as_string()
                elif inspect.isclass(default):
                    value = get_qualified_name(default, namespace)
                else:
                    value = str(default)
                tokens.append(Token(kind=TokenKind.LITERAL, value=value, has_suffix_space=False))
