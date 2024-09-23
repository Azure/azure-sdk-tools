import logging
import inspect
from collections import OrderedDict
import astroid
import re

from ._annotation_parser import FunctionAnnotationParser
from ._astroid_parser import AstroidFunctionParser
from ._docstring_parser import DocstringParser
from ._base_node import NodeEntityBase, get_qualified_name
from ._argtype import ArgType


# Find types like ~azure.core.paging.ItemPaged and group returns ItemPaged.
# Regex is used to find shorten such instances in complex type
# for e,g, ~azure.core.ItemPaged.ItemPaged[~azure.communication.chat.ChatThreadInfo] to ItemPaged[ChatThreadInfo]
REGEX_FIND_LONG_TYPE = r"((?:~?)[\\w.]+\.+([\\w]+))"


class FunctionNode(NodeEntityBase):
    """Function node class represents parsed function signature.
    Keyword args will be parsed and added to signature if docstring is available.
    :param str: namespace
    :param NodeEntityBase: parent_node
    :param function: obj
    :param astroid.FunctionDef: node
    :param bool: is_module_level
    """

    def __init__(self, namespace, parent_node, *, obj=None, node: astroid.FunctionDef=None, is_module_level=False):
        super().__init__(namespace, parent_node, obj)
        if not obj and node:
            self.name = node.name
            self.display_name = node.name
        self.annotations = []

        # Track **kwargs and *args separately, the way astroid does
        self.special_kwarg = None
        self.special_vararg = None

        self.args = OrderedDict()
        self.kwargs = OrderedDict()
        self.posargs = OrderedDict()

        self.return_type = None
        self.namespace_id = self.generate_id()
        # Set name space level ID as full name
        # Name space ID will be later updated for async methods
        self.full_name = self.namespace_id
        self.is_class_method = False
        self.is_module_level = is_module_level
        # Some of the methods wont be listed in API review
        # For e.g. ABC methods if class implements all ABC methods
        self.hidden = False
        try:
            self.node = node or astroid.extract_node(inspect.getsource(obj))
        except OSError:
            self.node = None
        self._inspect()
        self.kwargs = OrderedDict(sorted(self.kwargs.items()))

    def _inspect(self):
        logging.debug("Processing function {0}".format(self.name))

        self.is_async = isinstance(self.node, astroid.AsyncFunctionDef)
        self.def_key = "async def" if self.is_async else "def"

        # Update namespace ID to reflect async status. Otherwise ID will conflict between sync and async methods
        if self.is_async:
            self.namespace_id += ":async"
            self.full_name = self.namespace_id
        
        # Turn any decorators into annotation
        if self.node and self.node.decorators:
            self.annotations = [f"@{x.as_string(preserve_quotes=True)}" for x in self.node.decorators.nodes]

        self.is_class_method = "@classmethod" in self.annotations
        self._parse_function()

    def _parse_function(self):
        """ Find positional and keyword arguments, type and default value and return type of method."""
        # Add cls as first arg for class methods in API review tool
        if "@classmethod" in self.annotations:
            self.args["cls"] = ArgType(name="cls", argtype=None, default=inspect.Parameter.empty, keyword=None)

        if self.node:
            parser = AstroidFunctionParser(self.node, self.namespace, self)
        else:
            parser = FunctionAnnotationParser(self.obj, self.namespace, self)
        if parser:
            self.args = parser.args
            self.posargs = parser.posargs
            self.kwargs = parser.kwargs
            self.return_type = get_qualified_name(parser.return_type, self.namespace)
            self.special_kwarg = parser.special_kwarg
            self.special_vararg = parser.special_vararg
        self._parse_docstring()

    def _parse_docstring(self):
        # Parse docstring to get list of keyword args, type and default value for both positional and
        # kw args and return type( if not already found in signature)
        docstring = ""
        if hasattr(self.obj, "__doc__"):
            docstring = getattr(self.obj, "__doc__")
        # Refer docstring at class if this is constructor and docstring is missing for __init__
        if (
            not docstring
            and self.name == "__init__"
            and hasattr(self.parent_node.obj, "__doc__")
        ):
            docstring = getattr(self.parent_node.obj, "__doc__")

        if docstring:
            #  Parse doc string to find missing types, kwargs and return type
            parsed_docstring = DocstringParser(docstring)

            # Set return type if not already set
            if not self.return_type and parsed_docstring.ret_type:
                logging.debug(
                    "Setting return type from docstring for method {}".format(self.name)
                )
                self.return_type = parsed_docstring.ret_type

            # if something is missing from the signature parsing, update it from the
            # docstring, if available
            for argname, signature_arg in {**self.args, **self.posargs}.items():
                signature_arg.argtype = signature_arg.argtype if signature_arg.argtype is not None else parsed_docstring.type_for(argname)
                signature_arg.default = signature_arg.default if signature_arg.default is not None else  parsed_docstring.default_for(argname)

            # if something is missing from the signature parsing, update it from the
            # docstring, if available
            remaining_docstring_kwargs = set(parsed_docstring.kwargs.keys())
            for argname, kw_arg in self.kwargs.items():
                docstring_match = parsed_docstring.kwargs.get(argname, None)
                if not docstring_match:
                    continue
                remaining_docstring_kwargs.remove(argname)
                if not kw_arg.is_required:
                    kw_arg.argtype = kw_arg.argtype if kw_arg.argtype is not None else parsed_docstring.type_for(argname)
                    kw_arg.default = kw_arg.default if kw_arg.default is not None else parsed_docstring.default_for(argname)
            
            # ensure any kwargs described only in the docstrings are added
            for argname in remaining_docstring_kwargs:
                self.kwargs[argname] = parsed_docstring.kwargs[argname]

            # retrieve the special *args type from docstrings
            if self.special_kwarg and not self.special_kwarg.argtype:
                match = parsed_docstring.pos_args.get(self.special_kwarg.argname, None)
                if match:
                    self.special_kwarg.argtype = match.argtype

            # retrieve the special **kwargs type from docstrings
            if self.special_vararg and not self.special_vararg.argtype:
                match = parsed_docstring.pos_args.get(self.special_vararg.argname, None)
                if match:
                    self.special_vararg.argtype = match.argtype

    def _newline_if_needed(self, apiview, use_multi_line):
        if use_multi_line:
            apiview.add_newline()
            apiview.add_whitespace()

    def _argument_count(self) -> int:
        count = len(self.posargs) + len(self.args) + len(self.kwargs)
        if self.posargs:
            # account for /
            count += 1
        if self.kwargs:
            # account for *
            count += 1
        if self.special_kwarg:
            count += 1
        if self.special_vararg:
            count += 1
        return count

    def _generate_short_type(self, long_type):
        short_type = long_type
        groups = re.findall(REGEX_FIND_LONG_TYPE, short_type)
        for g in groups:
            short_type = short_type.replace(g[0], g[1])
        return short_type

    def _generate_args_for_collection(self, items, apiview, use_multi_line):
        for item in items.values():
            self._newline_if_needed(apiview, use_multi_line)
            item.generate_tokens(apiview, self.namespace_id, add_line_marker=use_multi_line)
            apiview.add_punctuation(",", False, True)

    def _generate_signature_token(self, apiview, use_multi_line):
        apiview.add_punctuation("(")

        if use_multi_line:
            # render errors directly below definition line
            for err in self.pylint_errors:
                err.generate_tokens(apiview, self.namespace_id)
            apiview.begin_group()
            apiview.begin_group()

        self._generate_args_for_collection(self.posargs, apiview, use_multi_line)
        # add postional-only marker if any posargs
        if self.posargs:
            self._newline_if_needed(apiview, use_multi_line)
            apiview.add_text("/")
            apiview.add_punctuation(",", False, True)

        self._generate_args_for_collection(self.args, apiview, use_multi_line)
        if self.special_vararg:
            self._newline_if_needed(apiview, use_multi_line)
            self.special_vararg.generate_tokens(apiview, self.namespace_id, add_line_marker=use_multi_line, prefix="*")
            apiview.add_punctuation(",", False, True)

        # add keyword argument marker        
        if self.kwargs:
            self._newline_if_needed(apiview, use_multi_line)
            apiview.add_text("*")
            apiview.add_punctuation(",", False, True)

        self._generate_args_for_collection(self.kwargs, apiview, use_multi_line)
        if self.special_kwarg:
            self._newline_if_needed(apiview, use_multi_line)
            self.special_kwarg.generate_tokens(apiview, self.namespace_id, add_line_marker=use_multi_line, prefix="**")
            apiview.add_punctuation(",", False, True)

        # pop the final ", " tokens
        if self._argument_count():
            apiview.tokens.pop()
            apiview.tokens.pop()

        if use_multi_line:
            apiview.add_newline()
            apiview.end_group()
            apiview.add_whitespace()
            apiview.add_punctuation(")")
            apiview.end_group()
        else:
            apiview.add_punctuation(")")

    def generate_tokens(self, apiview):
        """Generates token for function signature
        :param ApiView: apiview
        """
        # Show args in individual line if method has more than 4 args and use two tabs to properly aign them
        use_multi_line = self._argument_count() > 2

        parent_id = self.parent_node.namespace_id if self.parent_node else "???"
        logging.info(f"Processing method {self.name} in class {parent_id}")
        # Add tokens for annotations
        for annot in self.annotations:
            apiview.add_whitespace()
            apiview.add_keyword(annot)
            apiview.add_newline()

        apiview.add_whitespace()
        apiview.add_line_marker(self.namespace_id, add_cross_language_id=True)
        if self.is_async:
            apiview.add_keyword("async", False, True)

        apiview.add_keyword("def", False, True)
        # Show fully qualified name for module level function and short name for instance functions
        apiview.add_text(
            self.full_name if self.is_module_level else self.name,
            definition_id=self.namespace_id
        )
        # Add parameters
        self._generate_signature_token(apiview, use_multi_line)
        if self.return_type:
            apiview.add_punctuation("->", True, True)
            # Add line marker id if signature is displayed in multi lines
            if use_multi_line:
                line_id = f"{self.namespace_id}.returntype"
                apiview.add_line_marker(line_id)
            apiview.add_type(self.return_type)
        apiview.add_newline()
        if not use_multi_line:
            for err in self.pylint_errors:
                err.generate_tokens(apiview, self.namespace_id)            
