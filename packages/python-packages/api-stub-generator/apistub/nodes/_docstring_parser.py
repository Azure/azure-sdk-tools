from collections import OrderedDict
import re
import inspect
import logging
from ._argtype import ArgType


# REGEX to parse docstring
find_arg_regex = "(?<!:):{}\\s+([\w]*):(?!:)"
find_arg_and_type_regex = (
    "(?<!:):{}\\s+([~\w.]*[\[?[~\w.]*,?\\s?[~\w.]*\]?]?)\\s+([\w]*):(?!:)"
)
find_single_type_regex = "(?<!:):{0}\\s?{1}:[\\s]*([\S]+)(?!:)"
find_union_type_regex = (
    "(?<!:):{0}\\s?{1}:[\\s]*([\w.]*((\[[^\n]+\])|(\([^\n]+\))))(?!:)"
)
find_multi_type_regex = "(?<!:):({0})\\s?{1}:([^:]+)(?!:)"
find_docstring_return_type = "(?<!:):rtype\\s?:\\s+([^:\n]+)(?!:)"

# Regex to parse type hints
find_type_hint_ret_type = "(?<!#)\\s->\\s+([^\n:]*)"

# New types

line_tag_regex = re.compile(r"^\s*:([^:]+):(.*)")

docstring_types = ["param", "type", "paramtype", "keyword", "rtype"]

docstring_type_keywords = ["type", "vartype", "paramtype"]

docstring_param_keywords = ["param", "ivar", "keyword"]

docstring_return_keywords = ["rtype"]


class DocstringParser:
    """This represents a parsed doc string which has list of positional and keyword arguements and return type
    """

    def __init__(self, docstring):
        self.pos_args = OrderedDict()
        self.kw_args = OrderedDict()
        self.ivars = OrderedDict()
        self.ret_type = None
        self.docstring = docstring
        self._parse()


    def _process_arg_tuple(self, tag, line1, line2):
        # When two items are found, it is either the name
        # or the type. Example:
        # :param name: The name of the thing.
        # :type name: str
        #
        # This method has an inherent limitation that type info
        # can only span one extra line, not more than one.
        (keyword, label) = tag
        if keyword in docstring_param_keywords:
            arg = ArgType(name=label, argtype=None)
            self._update_arg(arg, keyword)
            return (arg, True)
        elif keyword in docstring_type_keywords:
            arg = self._arg_for_type(label, keyword)
            # If there's only useful text in the current or next line, we must
            # assume that line contains the type info.
            if line1 and not line2:
                arg.argtype = line1
            elif line2 and not line1:
                arg.argtype = line2
            elif line_tag_regex.match(line2):
                # if line2 can be parsed into a tag, it can't 
                # have extra type info for line1.
                arg.argtype = line1
            else:
                # TODO: When this assumption breaks down, you will need to revist...
                # Assume both lines contain type info and concatenate
                arg.argtype = " ".join([line1, line2])


    def _arg_for_type(self, name, keyword) -> ArgType:
        if keyword == "type":
            return self.pos_args[name]
        elif keyword == "vartype":
            return self.ivars[name]
        elif keyword == "paramtype":
            return self.kw_args[name]
        else:
            logging.error(f"Unexpected keyword {keyword}.")
            return None


    def _process_arg_triple(self, tag):
        # When three items are found, all necessary info is found
        # and there can only be one simple type
        # Example: :param str name: The name of the thing.
        (keyword, typename, name) = tag
        arg = ArgType(name=name, argtype=typename)
        self._update_arg(arg, keyword)


    def _update_arg(self, arg, keyword):
        if keyword == "ivar":
            self.ivars[arg.argname] = arg
        elif keyword == "param":
            self.pos_args[arg.argname] = arg
        elif keyword == "keyword":
            self.kw_args[arg.argname] = arg
        else:
            logging.error(f"Unexpected keyword: {keyword}")


    def _parse(self):
        """Parses a docstring without regex."""
        if not self.docstring:
            logging.error("Unable to parse empty docstring.")
            return

        lines = [x.replace("\n", "").strip() for x in self.docstring.splitlines()]
        for line_no, line in enumerate(lines):

            match = line_tag_regex.match(line)
            if not match:
                continue

            (tag, line1) = match.groups()
            split_tag = tag.split()
            if len(split_tag) == 3:
                self._process_arg_triple(split_tag)
            elif len(split_tag) == 2:
                # retrieve next line, if available
                try:
                    line2 = lines[line_no + 1].strip()
                except IndexError:
                    line2 = None
                self._process_arg_tuple(split_tag, line1.strip(), line2)


    def type_for(self, name):
        arg = (
            self.ivars.get(name, None) or
            self.pos_args.get(name, None) or
            self.kw_args.get(name, None)
        )
        return arg.argtype if arg else arg


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
            ret_type_val = ret_type.groups()[-1]
            # clean up return types spread over multiple lines
            ret_type_val = "".join([x.strip() for x in ret_type_val.splitlines()])
            return ret_type_val.replace(",", ", ").replace("  ", " ")

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

        self.pos_args = { x.argname: x for x in self.find_args("param") }
        self.kw_args = { x.argname: x for x in self.find_args("keyword") }
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
