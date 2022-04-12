import astroid
import inspect

# Special default values that should not be treated as string literal
SPECIAL_DEFAULT_VALUES = ["None", "..."]

# Lint warnings
TYPE_NOT_AVAILABLE = "Type is not available for {0}"

TYPE_NOT_REQUIRED = ["**kwargs", "self", "cls", "*", ]

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

    def generate_tokens(self, apiview, function_id, add_line_marker):
        """Generates token for the node and it's children recursively and add it to apiview
        :param ~ApiVersion apiview: The ApiView
        :param str function_id: Module level Unique ID created for function 
        :param bool include_default: Optional flag to indicate to include/exclude default value in tokens
        """
        # Add arg name
        self.id = function_id
        if add_line_marker:
            self.id = "{0}.param({1}".format(function_id, self.argname)
            apiview.add_line_marker(self.id)

        apiview.add_text(self.id, self.argname)
        # add arg type
        if self.argtype:
            apiview.add_punctuation(":", False, True)
            apiview.add_type(self.argtype, self.id)
        elif self.argname not in (TYPE_NOT_REQUIRED):
            # Type is not available. Add lint error in review
            error_msg = TYPE_NOT_AVAILABLE.format(self.argname)
            apiview.add_diagnostic(error_msg, self.id)
            if self.function_node:
                self.function_node.add_error(error_msg)

        # add arg default value
        default = self.default
        if default is not None:
            apiview.add_punctuation("=", True, True)
            if isinstance(default, str) and default not in SPECIAL_DEFAULT_VALUES:
                apiview.add_stringliteral(default)
            else:
                if isinstance(default, astroid.node_classes.Name):
                    value = default.name
                elif hasattr(default, "as_string"):
                    value = default.as_string()
                else:
                    value = str(default)
                apiview.add_literal(value)
