import astroid
import inspect
from typing import TYPE_CHECKING

from ._base_node import get_qualified_name
from .._generated.treestyle.parser.models import ReviewToken as Token, TokenKind

if TYPE_CHECKING:
    from .._generated.treestyle.parser.models._patch import ReviewLine

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

    def __init__(self, name, *, argtype, default, keyword, apiview, func_node=None):
        self.argname = name
        self.apiview = apiview
        if default == inspect.Parameter.empty:
            self.is_required = True
            self.default = None
        elif default is None:
            self.is_required = False
            self.default = "..." if keyword == "keyword" else "None"
        else:
            self.is_required = False
            self.default = default

        if argtype and all(
            [
                not self.is_required,
                self.default is None,
                not keyword in ["ivar", "param"],
                not argtype.startswith("Optional"),
            ]
        ):
            self.argtype = f"Optional[{argtype}]"
        else:
            self.argtype = argtype
        self.function_node = func_node

    def generate_tokens(
        self,
        function_id,
        namespace,
        review_line: "ReviewLine",
        *,
        add_line_marker: bool,
        prefix: str = "",
    ):
        """Generates token for the node and it's children recursively and add it to apiview
        :param str function_id: Module level Unique ID created for function
        :param str namespace: Namespace.
        :param ReviewLine review_line: Line to add tokens to.
        :keyword bool add_line_marker: Flag to indicate whether to include a line ID marker or not.
        :keyword str prefix: Optional prefix for *args and **kwargs.
        """
        # Add arg name
        self.id = function_id
        indent = ""
        if add_line_marker:
            self.id = f"{function_id}.param({self.argname})"
            review_line.add_line_marker(self.id)
            indent = " " * 4
        review_line.add_text(text=f"{indent}{prefix}{self.argname}", has_suffix_space=False)
        # add arg type
        if self.argtype:
            review_line.add_punctuation(":")
            review_line.add_type(self.argtype, apiview=self.apiview, has_suffix_space=False)

        # add arg default value
        default = self.default
        if default is not None:
            review_line.add_punctuation("=", has_prefix_space=True)
            if isinstance(default, str) and default not in SPECIAL_DEFAULT_VALUES:
                review_line.add_string_literal(default, has_suffix_space=False)
            else:
                if isinstance(default, astroid.node_classes.Name):
                    value = default.name
                elif hasattr(default, "as_string"):
                    value = default.as_string()
                elif inspect.isclass(default):
                    value = get_qualified_name(default, namespace)
                else:
                    value = str(default)
                review_line.add_literal(value, has_suffix_space=False)
