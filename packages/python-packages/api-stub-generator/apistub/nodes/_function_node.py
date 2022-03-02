import logging
import inspect
from collections import OrderedDict
import astroid
import re
from inspect import Parameter

from charset_normalizer import api
from ._docstring_parser import DocstringParser
from ._typehint_parser import TypeHintParser
from ._base_node import NodeEntityBase, get_qualified_name
from ._argtype import ArgType


VALIDATION_REQUIRED_DUNDER = ["__init__",]
KWARG_NOT_REQUIRED_METHODS = ["close",]
TYPEHINT_NOT_REQUIRED_METHODS = ["close", "__init__"]
REGEX_ITEM_PAGED = "(~[\w.]*\.)?([\w]*)\s?[\[\(][^\n]*[\]\)]"
PAGED_TYPES = ["ItemPaged", "AsyncItemPaged",]
# Methods that are implementation of known interface should be excluded from lint check
# for e.g. get, update, keys
LINT_EXCLUSION_METHODS = [
    "get",
    "has_key",
    "items",
    "keys",
    "update",
    "values",
    "close",    
]
# Find types like ~azure.core.paging.ItemPaged and group returns ItemPaged.
# Regex is used to find shorten such instances in complex type
# for e,g, ~azure.core.ItemPaged.ItemPaged[~azure.communication.chat.ChatThreadInfo] to ItemPaged[ChatThreadInfo]
REGEX_FIND_LONG_TYPE = "((?:~?)[\w.]+\.+([\w]+))"


def is_kwarg_mandatory(func_name):
    return not func_name.startswith("_") and func_name not in KWARG_NOT_REQUIRED_METHODS


def is_typehint_mandatory(func_name):
    return not func_name.startswith("_") and func_name not in TYPEHINT_NOT_REQUIRED_METHODS


class FunctionNode(NodeEntityBase):
    """Function node class represents parsed function signature.
    Keyword args will be parsed and added to signature if docstring is available.
    :param str: namespace
    :param NodeEntityBase: parent_node
    :param function: obj
    :param bool: is_module_level
    """

    def __init__(self, namespace, parent_node, obj, is_module_level=False):
        super().__init__(namespace, parent_node, obj)
        self.annotations = []
        self.args = OrderedDict()
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
        self._inspect()


    def _inspect(self):
        logging.debug("Processing function {0}".format(self.name))
        try:
            code = inspect.getsource(self.obj).strip()
        except OSError:
            # skip functions with no source code
            self.is_async = False
            return
        
        for line in code.splitlines():
            # skip decorators
            if line.strip().startswith("@"):
                continue
            # the first non-decorator line should be the function signature
            self.is_async = line.strip().startswith("async def")
            self.def_key = "async def" if self.is_async else "def"
            break

        # Update namespace ID to reflect async status. Otherwise ID will conflict between sync and async methods
        if self.is_async:
            self.namespace_id += ":async"

        # Find decorators and any annotations
        try:
            node = astroid.extract_node(inspect.getsource(self.obj))
            if node.decorators:
                self.annotations = [
                    "@{}".format(x.name)
                    for x in node.decorators.nodes
                    if hasattr(x, "name")
                ]
        except:
            # TODO: Update exception details in error
            error_message = "Error in parsing decorators for function {}".format(
                self.name
            )
            self.add_error(error_message)

        self.is_class_method = "@classmethod" in self.annotations
        self._parse_function()


    def _parse_function(self):
        """
        Find positional and keyword arguements, type and default value and return type of method
        Parsing logic will follow below order to identify these information
        1. Identify args, types, default and ret type using inspect
        2. Parse type annotations if inspect doesn't have complete info
        3. Parse docstring to find keyword arguements
        4. Parse type hints
        """
        # Add cls as first arg for class methods in API review tool
        if "@classmethod" in self.annotations:
            self.args["cls"] = ArgType(name="cls", default=Parameter.empty, keyword=None)

        # Find signature to find positional args and return type
        sig = inspect.signature(self.obj)
        params = sig.parameters
        # Add all keyword only args here temporarily until docstring is parsed
        # This is to handle the scenario for keyword arg typehint (py3 style is present in signature itself)
        self.kw_args = OrderedDict()
        for argname, argvalues in params.items():
            arg = ArgType(name=argname, argtype=get_qualified_name(argvalues.annotation, self.namespace), default=argvalues.default, func_node=self)

            # Store handle to kwarg object to replace it later
            if argvalues.kind == Parameter.VAR_KEYWORD:
                arg.argname = f"**{argname}"

            if argvalues.kind == Parameter.KEYWORD_ONLY:
                # Keyword-only args with "None" default are displayed as "..."
                # to match logic in docstring parsing
                if arg.default == None and not arg.is_required:
                    arg.default = "..."
                self.kw_args[arg.argname] = arg
            elif argvalues.kind == Parameter.VAR_POSITIONAL:
                # to work with docstring parsing, the key must
                # not have the * in it.
                arg.argname = f"*{argname}"
                self.args[argname] = arg
            else:
                self.args[arg.argname] = arg

        if sig.return_annotation:
            self.return_type = get_qualified_name(sig.return_annotation, self.namespace)

        self._parse_docstring()
        self._parse_typehint()
        self._order_final_args()

        if not self.return_type and is_typehint_mandatory(self.name):
            self.add_error("Return type is missing in both typehint and docstring")
        # Validate return type
        self._validate_pageable_api()


    def _order_final_args(self):
        # find and temporarily remove the kwargs param from arguments
        #  if present from the signature inspection
        kwargs_param = None
        kwargs_name = None
        if not kwargs_param:
            for argname in self.args:
                # find kwarg params with a different name, like config
                if argname.startswith("**"):
                    kwargs_name = argname
                    break
            if kwargs_name:
                kwargs_param = self.args.pop(kwargs_name, None)

        # add keyword args
        if self.kw_args:
            # Add separator to differentiate pos_arg and keyword args
            self.args["*"] = ArgType("*", default=Parameter.empty, keyword=None)
            for argname, arg in sorted(self.kw_args.items()):
                arg.function_node = self
                self.args[argname] = arg

        # re-append "**kwargs" to the end of the arguments list
        if kwargs_param:
            self.args[kwargs_name] = kwargs_param

        # API must have **kwargs for non async methods. Flag it as an error if it is missing for public API
        if not kwargs_param and is_kwarg_mandatory(self.name):
            self.errors.append("Keyword arg (**kwargs) is missing in method {}".format(self.name))


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

            # Update positional argument metadata from the docstring; otherwise, stick with
            # what was parsed from the signature.
            for argname, signature_arg in self.args.items():
                docstring_match = parsed_docstring.pos_args.get(argname, None)
                if not docstring_match:
                    continue
                signature_arg.argtype = docstring_match.argtype or signature_arg.argtype
                signature_arg.default = docstring_match.default or signature_arg.default

            # Update keyword argument metadata from the docstring; otherwise, stick with
            # what was parsed from the signature.
            remaining_docstring_kwargs = set(parsed_docstring.kw_args.keys())
            for argname, kw_arg in self.kw_args.items():
                docstring_match = parsed_docstring.kw_args.get(argname, None)
                if not docstring_match:
                    continue
                remaining_docstring_kwargs.remove(argname)
                if not kw_arg.is_required:
                    kw_arg.argtype = kw_arg.argtype or docstring_match.argtype 
                    kw_arg.default = kw_arg.default or docstring_match.default
            
            # ensure any kwargs described only in the docstrings are added
            for argname in remaining_docstring_kwargs:
                self.kw_args[argname] = parsed_docstring.kw_args[argname]


    def _generate_short_type(self, long_type):
        short_type = long_type
        groups = re.findall(REGEX_FIND_LONG_TYPE, short_type)
        for g in groups:
            short_type = short_type.replace(g[0], g[1])
        return short_type


    def _parse_typehint(self):

        # Skip parsing typehint if typehint is not expected for e.g dunder or async methods
        # and if return type is already found
        if self.return_type and not is_typehint_mandatory(self.name) or self.is_async:
            return

        # Parse type hint to get return type and types for positional args
        typehint_parser = TypeHintParser(self.obj)
        # Find return type from type hint if return type is not already set
        type_hint_ret_type = typehint_parser.ret_type
        # Type hint must be present for all APIs. Flag it as an error if typehint is missing
        if  not type_hint_ret_type:
            if (is_typehint_mandatory(self.name)):
                self.add_error("Typehint is missing for method {}".format(self.name))
            return

        if self.return_type:
            # Verify return type is same in docstring and typehint if typehint is available
            short_return_type = self._generate_short_type(self.return_type)
            long_ret_type = self.return_type
            if long_ret_type != type_hint_ret_type and short_return_type != type_hint_ret_type:
                logging.info("Long type: {0}, Short type: {1}, Type hint return type: {2}".format(long_ret_type, short_return_type, type_hint_ret_type))
                error_message = "The return type is described in both a type hint and docstring, but they do not match."
                self.add_error(error_message)
        # because the typehint isn't subject to the 2-line limit, prefer it over
        # a type parsed from the docstring.
        self.return_type = type_hint_ret_type or self.return_type


    def _generate_signature_token(self, apiview):
        apiview.add_punctuation("(")
        args_count = len(self.args)
        use_multi_line = args_count > 2
        # Show args in individual line if method has more than 4 args and use two tabs to properly aign them
        if use_multi_line:
            apiview.begin_group()
            apiview.begin_group()

        # Generate token for each arg
        for index, key in enumerate(self.args.keys()):
            # Add new line if args are listed in new line
            if use_multi_line:
                apiview.add_newline()
                apiview.add_whitespace()

            self.args[key].generate_tokens(
                apiview, self.namespace_id, use_multi_line
            )
            # Add punctuation between types except for last one
            if index < args_count - 1:
                apiview.add_punctuation(",", False, True)

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
        logging.info("Processing method {0} in class {1}".format(self.name, self.parent_node.namespace_id))
        # Add tokens for annotations
        for annot in self.annotations:
            apiview.add_whitespace()
            apiview.add_keyword(annot)
            apiview.add_newline()

        apiview.add_whitespace()
        apiview.add_line_marker(self.namespace_id)
        if self.is_async:
            apiview.add_keyword("async", False, True)

        apiview.add_keyword("def", False, True)
        # Show fully qualified name for module level function and short name for instance functions
        apiview.add_text(
            self.namespace_id, self.full_name if self.is_module_level else self.name,
            add_cross_language_id=True
        )
        # Add parameters
        self._generate_signature_token(apiview)
        if self.return_type:
            apiview.add_punctuation("->", True, True)
            # Add line marker id if signature is displayed in multi lines
            if len(self.args) > 2:
                line_id = "{}.returntype".format(self.namespace_id)
                apiview.add_line_marker(line_id)
            apiview.add_type(self.return_type)
        apiview.add_newline()

        if self.errors:
            for e in self.errors:
                apiview.add_diagnostic(e, self.namespace_id)


    def add_error(self, error_msg):
        # Ignore errors for lint check excluded methods
        if self.name in LINT_EXCLUSION_METHODS:
            return

        # Hide all diagnostics for now for dunder methods
        # These are well known protocol implementation
        if not self.name.startswith("_") or self.name in VALIDATION_REQUIRED_DUNDER:
            self.errors.append(error_msg)


    def _validate_pageable_api(self):
        # If api name starts with "list" and if annotated with "@distributed_trace"
        # then this method should return ItemPaged or AsyncItemPaged
        if self.return_type and self.name.startswith("list") and  "@distributed_trace" in self.annotations:
            tokens = re.search(REGEX_ITEM_PAGED, self.return_type)
            if tokens:
                ret_short_type = tokens.groups()[-1]
                if ret_short_type in PAGED_TYPES:
                    logging.debug("list API returns valid paged return type")
                    return
            error_msg = "list API {0} should return ItemPaged or AsyncItemPaged instead of {1} and page type must be included in docstring rtype".format(self.name, self.return_type)
            self.add_error(error_msg)                
        

    def print_errors(self):
        if self.errors:
            print("  method: {}".format(self.name))
            for e in self.errors:
                print("      {}".format(e))
