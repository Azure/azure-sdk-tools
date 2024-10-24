import logging
import inspect
from collections import OrderedDict
import astroid
import re
from typing import TYPE_CHECKING, List, Dict

from ._annotation_parser import FunctionAnnotationParser
from ._astroid_parser import AstroidFunctionParser
from ._docstring_parser import DocstringParser
from ._base_node import NodeEntityBase, get_qualified_name
from ._argtype import ArgType
from .._generated.treestyle.parser.models import ReviewToken as Token, TokenKind, add_review_line, set_blank_lines, add_type

if TYPE_CHECKING:
    from .._generated.treestyle.parser.models import ReviewLine


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
        self.children = []

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

    def _generate_args_for_collection(self, items: Dict[str, ArgType], review_lines, use_multi_line):
        for item in items.values():
            item.generate_tokens(review_lines, self.namespace_id, namespace=self.namespace, add_line_marker=use_multi_line)
            review_lines[-1]['Tokens'].append(Token(kind=TokenKind.PUNCTUATION, value=","))

    def _generate_signature_token(self, review_lines, tokens, use_multi_line):
        tokens.append(Token(kind=TokenKind.PUNCTUATION, value="(", has_suffix_space=False))

        # TODO: extra indent for all below
        use_multi_line = False
        if use_multi_line:
            # TODO: if the goal is to not add whitespace tokens, should indent be built into review line as well?
            add_review_line(
                review_lines=review_lines,
                line_id=self.namespace_id,
                tokens=tokens,
                #related_to_line=self.namespace_id,
                #add_cross_language_id=True     # TODO: add cross language id
            )
            # render errors directly below definition line
            for err in self.pylint_errors:
                err.generate_tokens(review_lines, self.namespace_id)
            #apiview.begin_group()
        else:
            add_review_line(
                review_lines=review_lines,
                tokens=tokens,
                #related_to_line=self.namespace_id,
                #add_cross_language_id=True     # TODO: add cross language id
            )

        self._generate_args_for_collection(self.posargs, review_lines, use_multi_line)
        # add postional-only marker if any posargs
        if self.posargs:
            tokens = [
                Token(kind=TokenKind.TEXT, value="/", has_suffix_space=False),
                Token(kind=TokenKind.PUNCTUATION, value=",")
            ]
            add_review_line(review_lines, tokens=tokens)

        self._generate_args_for_collection(self.args, review_lines, use_multi_line)
        if self.special_vararg:
            self.special_vararg.generate_tokens(review_lines, self.namespace_id, namespace=self.namespace, add_line_marker=use_multi_line, prefix="*")
            #apiview.add_punctuation(",", False, True)

        # add keyword argument marker        
        if self.kwargs and not self.special_vararg:
            tokens = []
            # TODO: only add this if self.special_vararg is not present
            tokens.append(Token(kind=TokenKind.TEXT, value="*", has_suffix_space=False))
            tokens.append(Token(kind=TokenKind.PUNCTUATION, value=","))
            add_review_line(review_lines, tokens=tokens)

        self._generate_args_for_collection(self.kwargs, review_lines, use_multi_line)
        if self.special_kwarg:
            tokens = []
            self.special_kwarg.generate_tokens(
                review_lines,
                self.namespace_id,
                self.namespace,
                add_line_marker=use_multi_line,
                prefix="**"
            )
            tokens.append(Token(kind=TokenKind.PUNCTUATION, value=","))
            add_review_line(review_lines, tokens=tokens)

        # pop the final ", " tokens
        #if self._argument_count():
        #    apiview.tokens.pop()
        #    apiview.tokens.pop()

        #apiview.add_newline()
        #apiview.end_group()
        #apiview.add_whitespace()
        tokens = []
        tokens.append(Token(kind=TokenKind.PUNCTUATION, value=")"))
        add_review_line(review_lines, tokens=tokens)

    def generate_tokens(self, review_lines: List["ReviewLine"]):
        """Generates token for function signature
        :param ApiView: apiview
        """
        # Show args in individual line if method has more than 4 args and use two tabs to properly align them
        use_multi_line = self._argument_count() > 2

        parent_id = self.parent_node.namespace_id if self.parent_node else "???"
        logging.info(f"Processing method {self.name} in class {parent_id}")
        # Add tokens for annotations
        for annot in self.annotations:
            add_review_line(
                review_lines=review_lines,
                tokens=[Token(kind=TokenKind.KEYWORD, value=annot, has_suffix_space=False)]
            )

        tokens = []
        if self.is_async:
            tokens.append(Token(kind=TokenKind.KEYWORD, value="async"))

        tokens.append(Token(kind=TokenKind.KEYWORD, value="def"))
        # Show fully qualified name for module level function and short name for instance functions
        value = self.full_name if self.is_module_level else self.name
        tokens.append(Token(kind=TokenKind.TEXT, value=value, has_suffix_space=False))
        # Add parameters
        self._generate_signature_token(review_lines, tokens, use_multi_line)

        if self.return_type:
            tokens = []
            # TODO: add has_prefix_space here
            tokens.append(Token(kind=TokenKind.PUNCTUATION, value="->"))
            # Add line marker id if signature is displayed in multi lines
            if use_multi_line:
                line_id = f"{self.namespace_id}.returntype"
            add_type(tokens, self.return_type)
        #if not use_multi_line:
        #    for err in self.pylint_errors:
        #        err.generate_tokens(apiview, self.namespace_id)            
