import re
import inspect
import logging
from ._argtype import ArgType


# REGEX to parse docstring
find_arg_regex = "(?<!:):{}\s+([\w]*):(?!:)"
find_arg_and_type_regex = (
    "(?<!:):{}\s+([~\w.]*[\[?[~\w.]*,?\s?[~\w.]*\]?]?)\s+([\w]*):(?!:)"
)
find_single_type_regex = "(?<!:):{0}\s?{1}:[\s]*([\S]+)(?!:)"
find_union_type_regex = (
    "(?<!:):{0}\s?{1}:[\s]*([\w.]*((\[[^\n]+\])|(\([^\n]+\))))(?!:)"
)
find_multi_type_regex = "(?<!:):({0})\s?{1}:([\s]*([\S]+)(\s+or\s+[\S]+)+)(?!:)"
find_docstring_return_type = "(?<!:):rtype\s?:\s+([^:\n]+)(?!:)"

# Regex to parse type hints
find_type_hint_ret_type = "(?<!#)#\stype:[\s\S]*->\s?([^\n]*)"

docstring_types = ["param", "type", "paramtype", "keyword", "rtype"]


class DocstringParser:
    """This represents a parsed doc string which has list of positional and keyword arguements and return type
    """

    def __init__(self, docstring):
        self.pos_args = []
        self.kw_args = []
        self.ret_type = None
        self.docstring = docstring

    def find_type(self, type_name="type", var_name=""):
        # This method will find argument type or return type from docstring
        # some params takes two types of params and some takes only one type
        # search for type strings with multiple type like below e.g.
        # :type <var_name>: <type1> or <type2>
        multi_type_regex = re.compile(find_multi_type_regex.format(type_name, var_name))
        type_groups = multi_type_regex.search(self.docstring)
        if type_groups:
            type_string = type_groups.groups()[2].replace("\n", "").strip()
            logging.debug("variable name: {0}, type from docstring: {1}".format(var_name, type_string))
            return type_string

        # Check for Union type
        union_type_regex = re.compile(find_union_type_regex.format(type_name, var_name))
        type_groups = union_type_regex.search(self.docstring)
        if type_groups:
            return type_groups.groups()[1]

        # Check for single type param
        # type e.g. :type <var_name>: <type1>
        single_type_regex = re.compile(
            find_single_type_regex.format(type_name, var_name)
        )
        type_groups = single_type_regex.search(self.docstring)
        if type_groups:
            return type_groups.groups()[-1]

        return None

    def find_return_type(self):
        # Find return type from docstring
        ret_type = re.search(find_docstring_return_type, self.docstring)
        if ret_type:
            return ret_type.groups()[-1]
        return None

    def find_args(self, arg_type="param"):
        # This method will find positional or kw arguement
        # find docstring that has both type and arg name in same docstring
        arg_type_regex = re.compile(find_arg_and_type_regex.format(arg_type))
        args = arg_type_regex.findall(self.docstring)
        params = [
            ArgType(x[1].strip(), x[0].strip())
            if x[1].strip()
            else ArgType(x[0].strip())
            for x in args
        ]

        # fin param or keyword args that ddoesn't type in same docstring
        arg_regex = re.compile(find_arg_regex.format(arg_type))
        args = arg_regex.findall(self.docstring)
        params.extend([ArgType(x.strip()) for x in args])

        # Get type if it is missing
        for p in params:
            # Show kwarg is optional by setting default to "..."
            if arg_type == "keyword":
                p.default = "..."

            if not p.argtype:
                p.argtype = self.find_type("(type|keywordtype|paramtype|vartype)", p.argname)
        return params

    def parse(self):
        """Returns a parsed docstring object
        """
        if not self.docstring:
            logging.error("Docstring is empty to parse")
            return

        self.pos_args = self.find_args("param")
        self.kw_args = self.find_args("keyword")
        self.ret_type = self.find_return_type()


class TypeHintParser:
    """TypeHintParser helps to find return type from type hint is type hint is available
    :param object: obj
    """

    def __init__(self, obj):
        self.obj = obj
        try:
            self.code = inspect.getsource(obj)
        except:
            self.code = None
            logging.error("Failed to get source of object {}".format(obj))

    def find_return_type(self):
        """Returns return type is type hint is available
        """
        if not self.code:
            return None

        # Find return type from type hint
        ret_type = re.search(find_type_hint_ret_type, self.code)
        # Don't return None as string literal
        if ret_type and ret_type != "None":
            return ret_type.groups()[-1]
        else:
            return None
