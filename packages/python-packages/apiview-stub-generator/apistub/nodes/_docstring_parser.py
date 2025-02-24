from collections import OrderedDict
import inspect
import logging
import re
from ._argtype import ArgType


line_tag_regex = re.compile(r"^\s*:([^:]+):(.*)")

default_patterns = [
    re.compile(r"\s+Default\s+value\s+is\s+([^\s]+)"),
    re.compile(r",\s+defaults\s+to\s+([^\s]+)"),
]

docstring_types = ["param", "type", "paramtype", "keyword", "rtype"]

docstring_type_keywords = ["type", "vartype", "paramtype"]

docstring_param_keywords = ["param", "ivar", "keyword"]

docstring_return_keywords = ["rtype"]


class DocstringParser:
    """This represents a parsed doc string which contain positional, instance, and keyword arguments
    and return type. Arguments are represented as key-value pairs where the key is the argument
    name and the value is the argument itself.
    """

    def __init__(self, docstring, apiview):
        self.pos_args = OrderedDict()
        self.kwargs = OrderedDict()
        self.ivars = OrderedDict()
        self.ret_type = None
        self.docstring = docstring
        self.apiview = apiview
        self._parse()

    def _extract_type(self, line1, line2):
        ret_val = ""
        line1 = line1.strip()
        if line1 == "":
            # if the first line is blank, the type info
            # must be on the second
            ret_val = line2
        elif line1.endswith(",") or line1.endswith(" or"):
            # if the first line ends with these values, the
            # type info wraps to the next line
            ret_val = " ".join([line1, line2])
        else:
            # otherwise, the type info is fully contained on
            # the first line
            ret_val = line1

        return self._sanitize_type(ret_val)

    def _extract_default(self, text):
        for pattern in default_patterns:
            match = pattern.search(text)
            if match:
                value = match[1]
                if value.endswith("."):
                    value = value[:-1]
                if value.startswith('"') and value.endswith('"'):
                    value = value[1:-1]
                return value
        return inspect.Parameter.empty

    def _sanitize_type(self, value):
        # strip unnecessary quotes from type strings
        for char in ['"', "'", "`"]:
            value = value.replace(char, "")
        return value

    def _process_arg_tuple(self, tag, line1, line2, default):
        # When two items are found, it is either the name
        # or the type. Example:
        # :param name: The name of the thing.
        # :type name: str
        #
        # This method has an inherent limitation that type info
        # can only span one extra line, not more than one.
        (keyword, label) = tag
        if keyword in docstring_param_keywords:
            arg = ArgType(name=label, argtype=None, default=default, keyword=keyword, apiview=self.apiview)
            self._update_arg(arg, keyword)
            return (arg, True)
        elif keyword in docstring_type_keywords:
            arg = self._arg_for_type(label, keyword)
            if arg:
                arg.argtype = self._extract_type(line1, line2)

    def _arg_for_type(self, name, keyword) -> ArgType:
        if keyword == "type":
            return self.pos_args.get(name, None)
        elif keyword == "vartype":
            return self.ivars.get(name, None)
        elif keyword == "paramtype":
            return self.kwargs.get(name, None)
        else:
            logging.error(f"Unexpected keyword {keyword}.")
            return None

    def _process_arg_triple(self, tag, default):
        # When three items are found, all necessary info is found
        # and there can only be one simple type
        # Example: :param str name: The name of the thing.
        (keyword, typename, name) = tag
        arg = ArgType(name=name, argtype=typename, default=default, keyword=keyword, apiview=self.apiview)
        self._update_arg(arg, keyword)

    def _process_return_type(self, line1, line2):
        return self._extract_type(line1, line2)

    def _update_arg(self, arg, keyword):
        if keyword == "ivar":
            self.ivars[arg.argname] = arg
        elif keyword == "param":
            self.pos_args[arg.argname] = arg
        elif keyword == "keyword":
            # show kwarg is optional by setting default to "..."
            # also wrap the type in Optional[] so it aligns with
            # optionals identified in type hints.
            # NOTE: docstring parser assumes all keyword arguments
            # are optional. Signature parsing takes precedence and
            # can tell the difference between required and optional
            # keyword aguments.
            arg.default = "..."
            if arg.argtype and not arg.argtype.startswith("Optional["):
                arg.argtype = f"Optional[{arg.argtype}]"
            self.kwargs[arg.argname] = arg
        else:
            logging.error(f"Unexpected keyword: {keyword}")

    """ From a given starting line number, find where the docstring
        description logically ends.
    """

    def _find_end_line(self, lines, start_at):
        # special case where the line being examined is the last line
        if start_at + 1 == len(lines):
            return start_at
        remaining = lines[start_at + 1 :]
        for idx, line in enumerate(remaining):
            if line_tag_regex.match(line):
                return start_at + idx + 1
        return start_at + len(remaining)

    def _parse(self):
        """Parses a docstring into an object."""
        if not self.docstring:
            logging.error("Unable to parse empty docstring.")
            return

        lines = [x.strip() for x in self.docstring.splitlines()]
        for line_no, line in enumerate(lines):

            tag_match = line_tag_regex.match(line)
            if not tag_match:
                continue

            end_line = self._find_end_line(lines, line_no)
            default = self._extract_default(" ".join(lines[line_no:end_line]))

            (tag, line1) = tag_match.groups()
            split_tag = tag.split()
            if len(split_tag) == 3:
                self._process_arg_triple(split_tag, default)
                continue

            # retrieve next line, if available
            try:
                line2 = lines[line_no + 1].strip()
            except IndexError:
                line2 = None

            if len(split_tag) == 2:
                self._process_arg_tuple(split_tag, line1.strip(), line2, default)
            elif len(split_tag) == 1 and split_tag[0] == "rtype":
                self.ret_type = self._process_return_type(line1.strip(), line2)

    def type_for(self, name):
        arg = self.ivars.get(name, None) or self.pos_args.get(name, None) or self.kwargs.get(name, None)
        return arg.argtype if arg else arg

    def default_for(self, name):
        arg = self.ivars.get(name, None) or self.pos_args.get(name, None) or self.kwargs.get(name, None)
        if not arg:
            return None
        argtype = arg.argtype or self.type_for(name)
        if not argtype:
            return arg.default
        try:
            # convert "None" to None if the type is optional.
            if argtype.startswith("Optional") and arg.default == "None":
                return None
            # if we have the default and the type, we should try to cast the
            # default to that type
            if argtype in ["bool", "Optional[bool]"]:
                return bool(arg.default)
            if argtype in ["int", "Optional[int]"]:
                return int(arg.default)
            if argtype in ["float", "Optional[float]"]:
                return float(arg.default)
            if argtype in ["complex", "Optional[complex]"]:
                return complex(arg.default)
            return arg.default
        except:
            # fall back to string if unable to parse
            return arg.default
