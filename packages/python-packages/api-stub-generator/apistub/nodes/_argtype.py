import logging

# Special default values that should not be treated as string literal
SPECIAL_DEFAULT_VALUES = ["None", "..."]


class ArgType:
    """Represents Argument type
    :param str name: Name of the argument
    :param str argtype: Type of the argument. for e.g. str, int, BlobBlock
    :param str default: Default value for the argument, If any
    """

    def __init__(self, name, argtype=None, default=None):
        self.argname = name
        self.argtype = argtype
        self.default = default

    def generate_tokens(self, apiview, function_id, add_line_marker):
        """Generates token for the node and it's children recursively and add it to apiview
        :param ~ApiVersion apiview: The ApiView
        :param str function_id: Module level Unique ID created for function 
        :param bool include_default: Optional flag to indicate to include/exclude default value in tokens
        """
        # Add arg name
        id = None
        if add_line_marker:
            id = "{0}.param({1}".format(function_id, self.argname)
            apiview.add_line_marker(id)

        apiview.add_text(id, self.argname)
        # add arg type
        if self.argtype:
            apiview.add_punctuation(":", False, True)
            apiview.add_type(self.argtype)

        # add arg default value
        if self.default:
            apiview.add_punctuation("=", True, True)
            # Add string literal or numeric literal based on the content within default
            # Ideally this should be based on arg type. But type is not available for all args
            # We should refer to arg type instead of content when all args have type
            if self.default in SPECIAL_DEFAULT_VALUES or self.argtype != "str":
                apiview.add_literal(self.default)
            else:
                apiview.add_stringliteral(self.default)
