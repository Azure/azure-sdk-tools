from collections import OrderedDict
import re
import logging
from ._argtype import ArgType


line_tag_regex = re.compile(r"^\s*:([^:]+):(.*)")

docstring_types = ["param", "type", "paramtype", "keyword", "rtype"]

docstring_type_keywords = ["type", "vartype", "paramtype"]

docstring_param_keywords = ["param", "ivar", "keyword"]

docstring_return_keywords = ["rtype"]


class DocstringParser:
    """This represents a parsed doc string which contain positional, instance, and keyword arguments
       and return type. Arguments are represented as key-value pairs where the key is the argument
       name and the value is the argument itself.
    """

    def __init__(self, docstring):
        self.pos_args = OrderedDict()
        self.kw_args = OrderedDict()
        self.ivars = OrderedDict()
        self.ret_type = None
        self.docstring = docstring
        self._parse()

    def _extract_type(self, line1, line2):
        line1 = line1.strip()
        if line1 == "":
            # if the first line is blank, the type info
            # must be on the second
            return line2
        if line1.endswith(",") or line1.endswith(" or"):
            # if the first line ends with these values, the 
            # type info wraps to the next line
            return " ".join([line1, line2])
        else:
            # otherwise, the type info is fully contained on
            # the first line
            return line1

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
            arg.argtype = self._extract_type(line1, line2)

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

    def _process_return_type(self, line1, line2):
        return self._extract_type(line1, line2)

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
        """Parses a docstring into an object."""
        if not self.docstring:
            logging.error("Unable to parse empty docstring.")
            return

        lines = [x.strip() for x in self.docstring.splitlines()]
        for line_no, line in enumerate(lines):

            tag_match = line_tag_regex.match(line)
            if not tag_match:
                continue

            (tag, line1) = tag_match.groups()
            split_tag = tag.split()
            if len(split_tag) == 3:
                self._process_arg_triple(split_tag)
                continue

            # retrieve next line, if available
            try:
                line2 = lines[line_no + 1].strip()
            except IndexError:
                line2 = None

            if len(split_tag) == 2:
                self._process_arg_tuple(split_tag, line1.strip(), line2)
            elif len(split_tag) == 1 and split_tag[0] == "rtype":
                self.ret_type = self._process_return_type(line1.strip(), line2)

    def type_for(self, name):
        arg = (
            self.ivars.get(name, None) or
            self.pos_args.get(name, None) or
            self.kw_args.get(name, None)
        )
        return arg.argtype if arg else arg
