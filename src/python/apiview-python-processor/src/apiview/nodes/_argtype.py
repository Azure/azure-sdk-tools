import logging

logging.getLogger().setLevel(logging.INFO)


class ArgType:
    """Represents Argument type
    :param str: name
        Name of the argument
    :param str: argtype
        Type of the argument. for e.g. str, int, BlobBlock
    :param str: default
        Default value for the argument, If any
    """

    def __init__(self, name, argtype = None, default = None):
        super().__init__()
        self.argname = name
        self.argtype = argtype
        self.default = default
        

    def dump(self, delim):
        space = ' ' * delim
        value = "{0}: {1}".format(self.argname, self.argtype) if self.argtype else self.argname
        if self.default:
            if isinstance(self.default, str) and self.default.isdigit():
                value += " = {}".format(self.default) 
            else:
                value += ' = "{}"'.format(self.default)

        print("{0}{1}".format(space, value))
            

    def generate_tokens(self, apiview, include_default = True):
        """Generates token for the node and it's children recursively and add it to apiview
        :param ApiView: apiview
        :param bool: include_default
            Optional flag to indicate to include/exclude default value in tokens
        """
        # Add arg name
        apiview.add_text("", self.argname)
        # add arg type
        if self.argtype:
            apiview.add_punctuation(":")
            apiview.add_space()
            apiview.add_type(self.argtype)

        # add arg default value
        if include_default and self.default:
            apiview.add_space()
            apiview.add_punctuation("=")
            apiview.add_space()
            # Add string literal or numeric literal based on the content within default
            # Ideally this should be based on arg type. But type is not available for all args
            # We should refer to arg type instead of content when all args have type
            if isinstance(self.default, str) and self.default.isdigit():
                apiview.add_literal(self.default)
            else:
                apiview.add_stringliteral(self.default)