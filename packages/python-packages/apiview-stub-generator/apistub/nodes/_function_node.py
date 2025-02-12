import logging
import inspect
from collections import OrderedDict
import astroid
import re
from typing import Dict

from ._annotation_parser import FunctionAnnotationParser
from ._astroid_parser import AstroidFunctionParser
from ._docstring_parser import DocstringParser
from ._base_node import NodeEntityBase, get_qualified_name
from ._argtype import ArgType
from .._generated.treestyle.parser.models import ReviewLines


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

    def __init__(
        self, namespace, parent_node, *, apiview, obj=None, node: astroid.FunctionDef = None, is_module_level=False
    ):
        super().__init__(namespace, parent_node, obj)
        if not obj and node:
            self.name = node.name
            self.display_name = node.name
        self.annotations = []
        self.children = ReviewLines()
        self.apiview = apiview

        # Track **kwargs and *args separately, the way astroid does
        self.special_kwarg = None
        self.special_vararg = None

        self.args = OrderedDict()
        self.kwargs = OrderedDict()
        self.posargs = OrderedDict()
        self.arg_count = 0

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
        """Find positional and keyword arguments, type and default value and return type of method."""
        # Add cls as first arg for class methods in API review tool
        if "@classmethod" in self.annotations:
            self.args["cls"] = ArgType(
                name="cls",
                argtype=None,
                default=inspect.Parameter.empty,
                keyword=None,
                apiview=self.apiview,
                func_node=self,
            )

        if self.node:
            parser = AstroidFunctionParser(self.node, self.namespace, apiview=self.apiview, func_node=self)
        else:
            parser = FunctionAnnotationParser(self.obj, self.namespace, apiview=self.apiview, func_node=self)
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
        if not docstring and self.name == "__init__" and hasattr(self.parent_node.obj, "__doc__"):
            docstring = getattr(self.parent_node.obj, "__doc__")

        if docstring:
            #  Parse doc string to find missing types, kwargs and return type
            parsed_docstring = DocstringParser(docstring, apiview=self.apiview)

            # Set return type if not already set
            if not self.return_type and parsed_docstring.ret_type:
                logging.debug("Setting return type from docstring for method {}".format(self.name))
                self.return_type = parsed_docstring.ret_type

            # if something is missing from the signature parsing, update it from the
            # docstring, if available
            for argname, signature_arg in {**self.args, **self.posargs}.items():
                signature_arg.argtype = (
                    signature_arg.argtype if signature_arg.argtype is not None else parsed_docstring.type_for(argname)
                )
                signature_arg.default = (
                    signature_arg.default
                    if signature_arg.default is not None
                    else parsed_docstring.default_for(argname)
                )

            # if something is missing from the signature parsing, update it from the
            # docstring, if available
            remaining_docstring_kwargs = set(parsed_docstring.kwargs.keys())
            for argname, kw_arg in self.kwargs.items():
                docstring_match = parsed_docstring.kwargs.get(argname, None)
                if not docstring_match:
                    continue
                remaining_docstring_kwargs.remove(argname)
                if not kw_arg.is_required:
                    kw_arg.argtype = (
                        kw_arg.argtype if kw_arg.argtype is not None else parsed_docstring.type_for(argname)
                    )
                    kw_arg.default = (
                        kw_arg.default if kw_arg.default is not None else parsed_docstring.default_for(argname)
                    )

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

    def _reviewline_if_needed(
        self,
        review_lines,
        review_line,
        use_multi_line,
        *,
        children=None,
    ):
        if use_multi_line:
            review_line.add_children(children)
            review_lines.append(review_line)
            # new token list for next line if multi-line
            review_line = review_lines.create_review_line()
        return review_line

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

    def _generate_args_for_collection(
        self, items: Dict[str, ArgType], review_lines, review_line, use_multi_line, *, final_item=True
    ):
        for idx, item in enumerate(list(items.values())):
            item.generate_tokens(
                self.namespace_id,
                namespace=self.namespace,
                review_line=review_line,
                add_line_marker=use_multi_line,
            )
            # if final_item is False, then items should not have commas
            if not final_item or idx < len(items) - 1:
                review_line.add_punctuation(",")
            # multi-line will create new list of tokens for next line
            review_line = self._reviewline_if_needed(review_lines, review_line, use_multi_line)
        return review_line

    def _generate_signature_token(self, review_lines, review_line, use_multi_line):
        review_line.add_punctuation("(", has_suffix_space=False)
        # if multi-line, then def tokens are parent tokens
        # to be used later when adding children
        def_line = review_line

        # If multi-line, then each param line will be a child.
        if use_multi_line:
            # render errors directly below definition line
            for err in self.pylint_errors:
                err.generate_tokens(self.apiview, self.namespace_id)
            param_lines = self.children
            review_line = review_lines.create_review_line(line_id=self.namespace_id)
        else:
            param_lines = review_lines

        # If length of positional args is less than total args, then all items should end with commas
        # as end of args list hasn't been reached. Else, last item reached, so no comma.
        current_count = len(self.posargs)
        final_item = current_count >= self.arg_count

        review_line = self._generate_args_for_collection(
            self.posargs,
            review_lines=param_lines,
            review_line=review_line,
            use_multi_line=use_multi_line,
            final_item=final_item,
        )
        # add postional-only marker if any posargs
        if self.posargs:
            # add extra indent manually for multi-line args
            indent = ""
            if use_multi_line:
                indent = " " * 4
            review_line.add_text(f"{indent}/", has_suffix_space=False)
            review_line.add_punctuation(",")
            current_count += 1  # account for /

            review_line = self._reviewline_if_needed(param_lines, review_line, use_multi_line)

        current_count += len(self.args)
        final_item = current_count >= self.arg_count

        review_line = self._generate_args_for_collection(
            self.args,
            review_lines=param_lines,
            review_line=review_line,
            use_multi_line=use_multi_line,
            final_item=final_item,
        )
        current_count += 1
        final_item = current_count >= self.arg_count
        if self.special_vararg:
            self.special_vararg.generate_tokens(
                self.namespace_id,
                namespace=self.namespace,
                review_line=review_line,
                add_line_marker=use_multi_line,
                prefix="*",
            )
            if not final_item:
                review_line.add_punctuation(",")
            review_line = self._reviewline_if_needed(param_lines, review_line, use_multi_line)

        # add keyword argument marker
        if self.kwargs:
            # TODO: https://github.com/Azure/azure-sdk-tools/issues/8574
            indent = ""
            if use_multi_line:
                indent = " " * 4
            review_line.add_text(f"{indent}*", has_suffix_space=False)
            review_line.add_punctuation(",")
            review_line = self._reviewline_if_needed(param_lines, review_line, use_multi_line)

        current_count += len(self.kwargs)
        final_item = current_count >= self.arg_count
        review_line = self._generate_args_for_collection(
            self.kwargs,
            review_lines=param_lines,
            review_line=review_line,
            use_multi_line=use_multi_line,
            final_item=final_item,
        )
        if self.special_kwarg:
            # if **kwargs is present, then no comma needed
            self.special_kwarg.generate_tokens(
                self.namespace_id,
                self.namespace,
                review_line,
                add_line_marker=use_multi_line,
                prefix="**",
            )
            review_line = self._reviewline_if_needed(param_lines, review_line, use_multi_line)

        review_line.add_punctuation(")", has_suffix_space=False)

        if self.return_type:
            review_line.add_punctuation("->", has_prefix_space=True)
            # Add line marker id if signature is displayed in multi lines
            if use_multi_line:
                line_id = f"{self.namespace_id}.returntype"
                review_line.add_line_marker(line_id)
            review_line.add_type(self.return_type, apiview=self.apiview, has_suffix_space=False)

        review_line = self._reviewline_if_needed(param_lines, review_line, use_multi_line)

        # after children are added, add the review line
        def_line.add_children(self.children)
        def_line.line_id = self.namespace_id
        review_lines.append(def_line)

    def generate_tokens(self, review_lines):
        """Generates token for function signature
        :param ApiView: apiview
        """
        # Show args in individual line if method has more than 4 args and use two tabs to properly align them
        self.arg_count = self._argument_count()
        use_multi_line = self.arg_count > 2

        parent_id = self.parent_node.namespace_id if self.parent_node else "???"
        logging.info(f"Processing method {self.name} in class {parent_id}")
        # Add tokens for annotations
        for annot in self.annotations:
            review_line = review_lines.create_review_line(related_to_line=self.namespace_id)
            review_line.add_keyword(annot, has_suffix_space=False)
            review_lines.append(review_line)
        review_line = review_lines.create_review_line()
        review_line.add_line_marker(self.namespace_id, add_cross_language_id=True, apiview=self.apiview)
        if self.is_async:
            review_line.add_keyword("async")

        review_line.add_keyword("def")
        # Show fully qualified name for module level function and short name for instance functions
        value = self.full_name if self.is_module_level else self.name
        # Add to navigation if module level function
        navigation_display_name = None
        if self.is_module_level:
            navigation_display_name = self.name
        review_line.add_text(
            value, has_suffix_space=False, navigation_display_name=navigation_display_name, render_classes=["method"]
        )
        # Add parameters
        review_line = self._generate_signature_token(review_lines, review_line, use_multi_line)
        # If multi-line function, mark blank line as context end.
        review_lines.set_blank_lines(last_is_context_end_line=use_multi_line)

        if not use_multi_line:
            for err in self.pylint_errors:
                err.generate_tokens(self.apiview, target_id=self.namespace_id)
