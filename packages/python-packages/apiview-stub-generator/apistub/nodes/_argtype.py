import astroid
import inspect

from ._base_node import get_qualified_name

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

    def generate_tokens(self, apiview, function_id, *, add_line_marker: bool, prefix: str = ""):
        """Generates token for the node and it's children recursively and add it to apiview
        :param ~ApiVersion apiview: The ApiView
        :param str function_id: Module level Unique ID created for function 
        :keyword bool add_line_marker: Flag to indicate whether to include a line ID marker or not.
        :keyword str prefix: Optional prefix for *args and **kwargs.
        """
        # Add arg name
        self.id = function_id
        if add_line_marker:
            self.id = f"{function_id}.param({self.argname})"
            apiview.add_line_marker(self.id)

        apiview.add_text(f"{prefix}{self.argname}")
        # add arg type
        if self.argtype:
            apiview.add_punctuation(":", False, True)
            apiview.add_type(self.argtype, self.id)

        # add arg default value
        default = self.default
        if default is not None:
            apiview.add_punctuation("=", True, True)
            if isinstance(default, str) and default not in SPECIAL_DEFAULT_VALUES:
                apiview.add_string_literal(default)
            else:
                if isinstance(default, astroid.node_classes.Name):
                    value = default.name
                elif hasattr(default, "as_string"):
                    value = default.as_string()
                elif inspect.isclass(default):
                    value = get_qualified_name(default, apiview.namespace)
                else:
                    value = str(default)
                apiview.add_literal(value)
