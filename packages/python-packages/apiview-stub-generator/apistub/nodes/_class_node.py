import ast
import astroid
import inspect
import logging
import operator
import sys
from enum import Enum
from typing import Dict, List, Optional, Tuple

from ._base_node import NodeEntityBase, get_qualified_name
from ._function_node import FunctionNode
from ._enum_node import EnumNode
from ._key_node import KeyNode
from ._property_node import PropertyNode
from ._docstring_parser import DocstringParser
from ._variable_node import VariableNode
from .._generated.treestyle.parser.models import ReviewLines
from .._parsing_helpers import parse_overloads, add_overload_nodes

# ---------------------------------------------------------------------------
# Per-file class source index and astroid node cache.
#
# inspect.getsource(cls) for a *class* does a full O(N_lines) linear scan of
# the source file on every call (CPython inspect.findsource).  For packages
# like azure-synapse-artifacts that put 900+ model classes in a single 3.6 MB
# file this balloons to 45+ minutes.  The helpers below replace that with:
#
#   _build_file_index  – reads + ast.parses a file ONCE, indexes all ClassDef
#                        nodes by qualified name and line range.
#   _get_class_source  – O(1) line-slice lookup after the first call per file.
#   _get_class_astroid_node – caches astroid.parse() result by (file, qualname)
#                             so the two independent calls in _parse_decorators
#                             and _parse_functions share one parse result, and
#                             MRO ancestors shared across many classes are only
#                             parsed once.
#   clear_caches       – resets all dicts; called at the start of each
#                        StubGenerator._generate_tokens() run so the test suite
#                        and multi-package runs stay correct.
# ---------------------------------------------------------------------------

# file_path -> {qualname: extracted_source_text}  (precomputed in _build_file_index)
_FILE_CLASS_SOURCE: Dict[str, Dict[str, str]] = {}
# file_path -> {qualname: (start_1based, end_1based)}  (1-based inclusive line range)
_FILE_CLASS_LINES: Dict[str, Dict[str, Tuple[int, int]]] = {}
# (file_path, qualname) -> Optional[astroid.ClassDef]
_CLASS_ASTROID_CACHE: Dict[Tuple[Optional[str], Optional[str]], Optional[object]] = {}


def _collect_class_defs(node, parent_qualname: str = ""):
    """Recursively yield (qualname, start_0based, end_exclusive) for every
    ClassDef in an ast tree, including nested classes."""
    for child in ast.iter_child_nodes(node):
        if isinstance(child, ast.ClassDef):
            qualname = f"{parent_qualname}.{child.name}" if parent_qualname else child.name
            # Include any decorators that precede the 'class' keyword.
            start_1based = (
                child.decorator_list[0].lineno
                if child.decorator_list
                else child.lineno
            )
            # end_lineno is 1-based inclusive; convert to 0-based exclusive.
            yield qualname, start_1based - 1, child.end_lineno
            yield from _collect_class_defs(child, qualname)


def _build_file_index(file_path: str) -> None:
    """Read *file_path* once, build a {qualname: source_text} map for all
    ClassDef nodes, and store the result in _FILE_CLASS_SOURCE."""
    try:
        with open(file_path, "r", encoding="utf-8", errors="replace") as fh:
            source = fh.read()
    except OSError:
        _FILE_CLASS_SOURCE[file_path] = {}
        _FILE_CLASS_LINES[file_path] = {}
        return
    # Split lines ONCE so we can slice cheaply for each class.
    lines = source.splitlines(keepends=True)
    src_index: Dict[str, str] = {}
    line_index: Dict[str, Tuple[int, int]] = {}
    try:
        tree = ast.parse(source)
        for qualname, start, end in _collect_class_defs(tree):
            # Precompute and store the source slice — O(1) lookup later.
            src_index[qualname] = "".join(lines[start:end])
            # Store 1-based inclusive line range for pylint error matching.
            line_index[qualname] = (start + 1, end)
    except SyntaxError:
        pass
    _FILE_CLASS_SOURCE[file_path] = src_index
    _FILE_CLASS_LINES[file_path] = line_index


def _get_class_source(cls) -> Optional[str]:
    """Return the source text for *cls* without inspect.getsource's O(N_lines)
    scan.  Falls back to inspect.getsource for dynamic / C-extension classes."""
    file_path = inspect.getsourcefile(cls)
    if not file_path:
        try:
            return inspect.getsource(cls)
        except Exception:
            return None
    if file_path not in _FILE_CLASS_SOURCE:
        _build_file_index(file_path)
    qualname = getattr(cls, "__qualname__", None) or getattr(cls, "__name__", None)
    if qualname:
        result = _FILE_CLASS_SOURCE[file_path].get(qualname)
        if result is not None:
            return result
    # Not found in index (dynamic class, or qualname mismatch) – slow fallback.
    try:
        return inspect.getsource(cls)
    except Exception:
        return None


def _get_class_line_range(cls) -> Tuple[Optional[int], Optional[int]]:
    """Return (start_1based, end_1based) for *cls* using the pre-built file index.

    Used by PylintParser.match_items to avoid the O(N_lines) inspect.getsourcelines
    scan for class objects.  Returns (None, None) when the class cannot be located.
    """
    file_path = inspect.getsourcefile(cls)
    if not file_path:
        return None, None
    if file_path not in _FILE_CLASS_LINES:
        _build_file_index(file_path)
    qualname = getattr(cls, "__qualname__", None) or getattr(cls, "__name__", None)
    if qualname:
        result = _FILE_CLASS_LINES[file_path].get(qualname)
        if result is not None:
            return result
    return None, None


def _get_class_astroid_node(cls) -> Optional["astroid.ClassDef"]:
    """Return the cached astroid ClassDef node for *cls*, parsing it at most
    once per (file_path, qualname) pair across the whole generator run."""
    file_path = inspect.getsourcefile(cls)
    qualname = getattr(cls, "__qualname__", None) or getattr(cls, "__name__", None)
    cache_key = (file_path, qualname)
    if cache_key in _CLASS_ASTROID_CACHE:
        return _CLASS_ASTROID_CACHE[cache_key]  # type: ignore[return-value]
    source = _get_class_source(cls)
    result = None
    if source:
        try:
            parsed = astroid.parse(source)
            if parsed.body and isinstance(parsed.body[0], astroid.ClassDef):
                result = parsed.body[0]
        except Exception:
            pass
    # Only cache when we have a stable key (i.e. not a dynamic class with no file).
    if file_path is not None:
        _CLASS_ASTROID_CACHE[cache_key] = result
    return result


def clear_caches() -> None:
    """Reset all module-level source and AST caches.

    Called at the start of each StubGenerator._generate_tokens() run so that
    back-to-back runs (e.g. in the test suite) do not share stale entries.
    """
    _FILE_CLASS_SOURCE.clear()
    _FILE_CLASS_LINES.clear()
    _CLASS_ASTROID_CACHE.clear()


find_keys = lambda x: isinstance(x, KeyNode)
find_props = lambda x: isinstance(x, PropertyNode)
find_instancefunc = (
    lambda x: isinstance(x, FunctionNode)
    and not x.is_class_method
    and not x.name.startswith("__")
)
find_enum = lambda x: isinstance(x, EnumNode)
find_var = lambda x: isinstance(x, VariableNode)
find_classfunc = lambda x: isinstance(x, FunctionNode) and x.is_class_method
find_dunder_func = lambda x: isinstance(x, FunctionNode) and x.name.startswith("__")

# This static dict will be used to identify if a class implements a specific ABC class
# and tag class as implementing corresponding ABC class instead of showing these dunder methods
ABSTRACT_CLASS_METHODS = {
    "ContextManager": ["__enter__", "__exit__"],
    "AsyncContextManager": ["__aenter__", "__aexit__"],
    "Iterator": ["__next__", "__iter__"],
    "Collection": ["__contains__", "__iter__", "__len__"],
    "Mapping": [
        "__getitem__",
        "__len__",
        "__eq__",
        "__ne__",
        "__contains__",
        "__iter__",
        "__len__",
        "keys",
        "items",
        "values",
        "get",
    ],
    "AsyncIterable": ["__anext__", "__aiter__"],
    "AsyncIterator": ["__anext__", "__aiter__"],
    "Awaitable": ["__await__"],
}


class ClassNode(NodeEntityBase):
    """Class node to represent parsed class node and children"""

    def __init__(
        self,
        *,
        name,
        namespace,
        parent_node,
        obj,
        pkg_root_namespace,
        apiview,
        allow_list=None,
    ):
        super().__init__(namespace, parent_node, obj)
        self.base_class_names = []
        self.class_keywords = []  # Store keyword arguments like metaclass=, total=
        # This is the name obtained by NodeEntityBase from __name__.
        # We must preserve it to detect the mismatch and issue a warning.
        self.children = ReviewLines()
        self.name = name
        self.namespace_id = self.generate_id()
        self.full_name = self.namespace_id
        self.implements = []
        self.pkg_root_namespace = pkg_root_namespace
        self.apiview = apiview
        self._allow_list = allow_list or []
        self._inspect()
        self._set_abc_implements()
        self._sort_elements()

    def _set_abc_implements(self):
        # Check if class adher to any abstract class implementation.
        # If class has implementation for all required abstract methods then tag this class
        # as implementing that abstract class. For e.g. if class has both __iter__ and __next__
        # then this class implements Iterator
        instance_functions = [
            x for x in self.child_nodes if isinstance(x, FunctionNode)
        ]
        instance_function_names = [x.name for x in instance_functions]

        is_implemented = lambda func: func in instance_function_names
        for c in ABSTRACT_CLASS_METHODS:
            logging.debug("Checking if class implements {}".format(c))
            if all(map(is_implemented, ABSTRACT_CLASS_METHODS[c])):
                logging.debug("Class {0} implements {1}".format(self.name, c))
                self.implements.append(c)

        # Hide all methods for implemented ABC classes/ implements
        methods_to_hide = []
        for abc_class in self.implements:
            methods_to_hide.extend(ABSTRACT_CLASS_METHODS[abc_class])
        # Hide abc methods for ABC implementations
        for method in instance_functions:
            if method.name in methods_to_hide:
                method.hidden = True

    def _should_include_function(self, func_obj):
        # Method or Function member should only be included if it is defined in same package.
        # So this check will filter any methods defined in parent class if parent class is in non-azure package
        # for e.g. as_dict method in msrest
        if not (inspect.ismethod(func_obj) or inspect.isfunction(func_obj)):
            return False
        if hasattr(func_obj, "__module__"):
            function_module = getattr(func_obj, "__module__")
            # TODO: Remove the "_model_base" workaround when this stuff is moved into azure-core.
            if not (
                function_module
                and function_module.startswith(self.pkg_root_namespace)
                and not (function_module.endswith("_model_base") or function_module.endswith("model_base"))
            ):
                return False
            # For dynamically-created classes (e.g. make_dataclass) that have no source file,
            # only include functions that themselves have a real source file. This filters out
            # generated methods like __init__ that were synthesized by the dataclass machinery,
            # while keeping user-defined methods (e.g. lambdas passed via namespace=).
            if not self._class_has_source:
                sourcefile = inspect.getsourcefile(func_obj)
                if not sourcefile or sourcefile == "<string>":
                    return False
            return True
        return False

    def _handle_variable(self, child_obj, name, *, type_string=None, value=None):
        allowed_types = (str, int, dict, list, float, bool)
        if not isinstance(child_obj, allowed_types):
            return

        # if variable is already present in parsed list then just update the value
        var_match = [
            v
            for v in self.child_nodes
            if isinstance(v, VariableNode) and v.name == name
        ]
        if var_match:
            if value:
                var_match[0].value = value
            if type_string:
                var_match[0].type = type_string
        else:
            is_ivar = True
            if type_string:
                is_ivar = not type_string.startswith("ClassVar")
            self.child_nodes.append(
                VariableNode(
                    namespace=self.namespace,
                    parent_node=self,
                    name=name,
                    type_name=type_string,
                    value=value,
                    is_ivar=is_ivar,
                )
            )

    def _parse_decorators_from_class(self, class_obj):
        try:
            class_node = _get_class_astroid_node(class_obj)
            if class_node and class_node.decorators:
                self.decorators = [
                    f"@{x.as_string(preserve_quotes=True)}" for x in class_node.decorators.nodes
                ]
            else:
                self.decorators = []
        except:
            self.decorators = []

    def _parse_functions_from_class(self, class_obj) -> List[astroid.FunctionDef]:
        try:
            class_node = _get_class_astroid_node(class_obj)
            if class_node:
                return [x for x in class_node.body if isinstance(x, astroid.FunctionDef)]
            return []
        except:
            return []

    def _inspect(self):
        # Inspect current class and it's members recursively
        logging.debug("Inspecting class {}".format(self.full_name))

        # get base classes
        self.base_class_names = self._get_base_classes()
        # Check if Enum is in Base class hierarchy
        self.is_enum = Enum in getattr(self.obj, "__mro__", [])
        # Find any ivar from docstring
        self._parse_ivars()

        is_typeddict = hasattr(self.obj, "__required_keys__") or hasattr(
            self.obj, "__optional_keys__"
        )

        self._parse_decorators_from_class(self.obj)

        # Cache whether this class has a Python source file. Used by _should_include_function()
        # to avoid repeated filesystem reads for every member of the class.
        # Use getsourcefile (O(1)) rather than getsource (O(N_lines) for classes) to avoid
        # re-introducing the slow linear scan that _get_class_source was designed to replace.
        # In Python 3.14+, NewType objects are not classes/functions and raise TypeError in
        # inspect.getfile(), so we guard here.
        try:
            self._class_has_source = inspect.getsourcefile(self.obj) is not None
        except TypeError:
            self._class_has_source = False

        # find members in node
        # enums with duplicate values are screened out by "getmembers" so
        # we must rely on __members__ instead.
        if hasattr(self.obj, "__members__"):
            try:
                members = self.obj.__members__.items()
            except AttributeError:
                members = inspect.getmembers(self.obj)
        else:
            members = inspect.getmembers(self.obj)

        try:
            mro = inspect.getmro(self.obj)
        except AttributeError:
            mro = (self.obj,)
        overloads_by_class = {
            cls: parse_overloads(self, self._parse_functions_from_class(cls), is_module_level=False)
            for cls in mro[:-1]
        }

        # PEP 649 (Python 3.14+): annotations are now lazy; __annotations__ is no longer
        # eagerly populated in cls.__dict__. inspect.get_annotations() resolves string
        # annotations from PEP 563 (from __future__ import annotations) via eval_str=True.
        try:
            own_annotations = inspect.get_annotations(self.obj, eval_str=True)
        except Exception:
            own_annotations = inspect.get_annotations(self.obj)
        for item_name, item_type in own_annotations.items():
            if item_name.startswith("_"):
                continue
            if is_typeddict and (
                inspect.isclass(item_type)
                or getattr(item_type, "__module__", None) == "typing"
            ):
                self.child_nodes.append(
                    KeyNode(self.namespace, self, item_name, item_type)
                )
            else:
                type_string = get_qualified_name(item_type, self.namespace)
                self._handle_variable({}, item_name, type_string=type_string)

        for name, child_obj in members:
            if inspect.isbuiltin(child_obj):
                continue
            elif self._should_include_function(child_obj):
                # Include dunder and public methods.
                # PEP 649 (Python 3.14+): skip __annotate_func__, an internal callable
                # exposed by inspect.getmembers(). Its __name__ is "__annotate__", so
                # check the callable's own name rather than the member-dict key.
                func_name = getattr(child_obj, '__name__', name)
                if (not name.startswith("_") or name.startswith("__")) and func_name != "__annotate__":
                    func_node = FunctionNode(
                        self.namespace, self, obj=child_obj, apiview=self.apiview
                    )
                    # Resolve overloads from the class that defines this method to
                    # avoid duplicates when a derived class redefines an overloaded base method.
                    defining_class = next((c for c in mro if name in c.__dict__), self.obj)
                    add_overload_nodes(self, func_node, overloads_by_class.get(defining_class, []))

            # now that we've looked at the specific dunder properties we are
            # willing to include, anything with a leading underscore should be ignored.
            if name.startswith("_"):
                continue

            if self.is_enum and isinstance(child_obj, self.obj):
                self.child_nodes.append(
                    EnumNode(
                        name=name,
                        namespace=self.namespace,
                        parent_node=self,
                        obj=child_obj,
                    )
                )
            elif inspect.isclass(child_obj):
                self.child_nodes.append(
                    ClassNode(
                        name=getattr(child_obj, "name", child_obj.__name__),
                        namespace=self.namespace,
                        parent_node=self,
                        obj=child_obj,
                        pkg_root_namespace=self.pkg_root_namespace,
                        apiview=self.apiview,
                    )
                )
            elif isinstance(child_obj, property):
                if not name.startswith("_"):
                    # Add instance properties
                    self.child_nodes.append(
                        PropertyNode(self.namespace, self, name, child_obj)
                    )
            else:
                self._handle_variable(child_obj, name, value=str(child_obj))

    def _parse_ivars(self):
        # This method will add instance variables by parsing docstring
        if not hasattr(self.obj, "__doc__"):
            return
        docstring = getattr(self.obj, "__doc__")
        if docstring:
            docstring_parser = DocstringParser(docstring, apiview=self.apiview)
            for key, var in docstring_parser.ivars.items():
                ivar_node = VariableNode(
                    namespace=self.namespace,
                    parent_node=self,
                    name=key,
                    type_name=var.argtype,
                    value=None,
                    is_ivar=True,
                )
                self.child_nodes.append(ivar_node)

    def _sort_elements(self):
        # Sort elements in following order
        # properties, variables, Enums, dunder methods, class functions and instance methods
        # sort all elements based on name firstand then group them
        self.child_nodes.sort(key=operator.attrgetter("name"))
        sorted_children = list(filter(find_keys, self.child_nodes))
        sorted_children.extend(filter(find_props, self.child_nodes))
        sorted_children.extend(filter(find_var, self.child_nodes))
        sorted_children.extend(filter(find_enum, self.child_nodes))
        sorted_children.extend(filter(find_dunder_func, self.child_nodes))
        sorted_children.extend(filter(find_classfunc, self.child_nodes))
        sorted_children.extend(filter(find_instancefunc, self.child_nodes))
        self.child_nodes = sorted_children

    @staticmethod
    def _unparse_without_quotes(node):
        """Unparse an AST node, replacing string constants (forward references) with their value."""
        class _ForwardRefToName(ast.NodeTransformer):
            def visit_Constant(self, node):
                if isinstance(node.value, str):
                    return ast.Name(id=node.value, ctx=ast.Load())
                return node

        return ast.unparse(_ForwardRefToName().visit(node))

    def _extract_bases_and_keywords_from_ast(self, class_node):
        """Extract base class names and keyword arguments from an ast.ClassDef node."""
        base_classes = [
            self._unparse_without_quotes(base)
            for base in class_node.bases
            if ast.unparse(base) != "object"
        ]
        keywords = [
            f"{kw.arg}={ast.unparse(kw.value)}"
            for kw in class_node.keywords
            if not ast.unparse(kw.value).lstrip(".").startswith("_")
        ]
        return base_classes, keywords

    def _get_base_classes(self):
        # Try to resolve from source (AST) to preserve exact names as written in source.
        # Falls back to runtime introspection if source is unavailable.
        #
        # Use _get_class_source() (O(1) via pre-built file index) instead of
        # inspect.getsource() which does an O(N_lines) scan for every class.

        # Attempt 1: parse source for just this class directly (O(1) via cache)
        try:
            source = _get_class_source(self.obj)
            if source:
                class_node = ast.parse(source).body[0]
                if isinstance(class_node, ast.ClassDef):
                    base_classes, self.class_keywords = self._extract_bases_and_keywords_from_ast(class_node)
                    if base_classes or self.class_keywords:
                        return base_classes
        except Exception as e:
            logging.debug(f"Direct AST parsing failed for {self.name}: {e}")

        # Attempt 2: look up the class in the already-built file index without re-reading the
        # module. This covers enum classes where inspect.getsource may resolve to the instance.
        try:
            module_name = self.obj.__module__
            if module_name and module_name in sys.modules:
                module = sys.modules[module_name]
                file_path = inspect.getsourcefile(module)
                if file_path:
                    if file_path not in _FILE_CLASS_SOURCE:
                        _build_file_index(file_path)
                    file_index = _FILE_CLASS_SOURCE.get(file_path, {})
                    qualname = getattr(self.obj, "__qualname__", None) or self.name
                    source = file_index.get(qualname) or file_index.get(self.name)
                    if source:
                        class_node = ast.parse(source).body[0]
                        if isinstance(class_node, ast.ClassDef):
                            base_classes, self.class_keywords = self._extract_bases_and_keywords_from_ast(class_node)
                            if base_classes or self.class_keywords:
                                return base_classes
        except Exception as e:
            logging.debug(f"Module-level AST parsing failed for {self.name}: {e}")

        # Fall back to runtime introspection if source parsing fails or yields no bases
        base_classes = []
        # Functional TypedDicts (e.g. Foo = TypedDict("Foo", {..})) have __required_keys__
        # but in Python < 3.12 their __orig_bases__ / __bases__ resolve to dict, not TypedDict.
        # Normalize to TypedDict so output is consistent across Python versions.
        is_typeddict = hasattr(self.obj, "__required_keys__")
        if is_typeddict:
            return ["TypedDict"]
        bases = getattr(self.obj, "__orig_bases__", [])
        if not bases:
            bases = getattr(self.obj, "__bases__", [])
        if not bases:
            supertype = getattr(self.obj, "__supertype__", None)
            if supertype:
                bases = [supertype]
        for cl in [c for c in bases or [] if c is not object]:
            base_classes.append(get_qualified_name(cl, self.namespace))
        return base_classes

    def is_pylint_error_owner(self, err) -> bool:
        """Check if this class node is the owner of a pylint error.

        For enum classes, exclude errors with column > 0 since those belong to enum values.

        :param PylintError err: The pylint error to check
        :return: True if this node owns the error, False otherwise
        :rtype: bool
        """
        # Check if this is an enum class and the error has column > 0 (enum value error)
        if self.is_enum and err.column > 0:
            # This error belongs to an enum value, not the class
            return False
        # Use default behavior for other cases
        return super().is_pylint_error_owner(err)

    def generate_tokens(self, review_lines: "ReviewLines"):
        """Generates token for the node and it's children recursively and add it to apiview
        :param review_lines: List of ReviewLine
        """
        logging.info(f"Processing class {self.namespace_id}")
        for decorator in self.decorators:
            line = review_lines.create_review_line(related_to_line=self.namespace_id, is_handwritten=self.is_handwritten)
            line.add_keyword(decorator, has_suffix_space=False)
            review_lines.append(line)

        line = review_lines.create_review_line(is_handwritten=self.is_handwritten)
        line.add_line_marker(
            self.namespace_id, add_cross_language_id=True, apiview=self.apiview
        )
        # Generate class name line
        line.add_keyword("class")
        if self.is_enum:
            render_classes = ["enum"]
        else:
            render_classes = ["class"]
        # TODO: #9454 - Change below to self.name once sticky parent node context window feature is added
        line.add_text(
            self.full_name,
            has_suffix_space=False,
            navigation_display_name=self.name,
            render_classes=render_classes,
        )

        for err in self.pylint_errors:
            err.generate_tokens(self.apiview, target_id=self.namespace_id)

        # Add inherited base classes and keywords
        if self.base_class_names or self.class_keywords:
            line.add_punctuation("(", has_suffix_space=False)

            # Add base classes
            if self.base_class_names:
                self._generate_tokens_for_collection(
                    self.base_class_names, line, has_suffix_space=False
                )

            # Add comma before keywords if we have both bases and keywords
            if self.base_class_names and self.class_keywords:
                line.add_punctuation(",")

            # Add keyword arguments (e.g., metaclass=..., total=...)
            for idx, keyword in enumerate(self.class_keywords):
                # Parse keyword as "arg=value"
                if "=" in keyword:
                    arg, value = keyword.split("=", 1)
                    line.add_text(arg, has_suffix_space=False)
                    line.add_punctuation("=", has_suffix_space=False)
                    # Use add_literal for Python literal values (True/False/None/numbers/strings)
                    # so they render with the correct token kind rather than as type names.
                    try:
                        ast.literal_eval(value)
                        line.add_literal(value, has_suffix_space=False)
                    except (ValueError, SyntaxError):
                        line.add_type(value, apiview=self.apiview, has_suffix_space=False)
                    # Add comma between multiple keywords
                    if idx < len(self.class_keywords) - 1:
                        line.add_punctuation(",")

            line.add_punctuation(")", has_suffix_space=False)
        line.add_punctuation(":", has_suffix_space=False)
        # Add any ABC implementation list
        if self.implements:
            line.add_text(" ", has_suffix_space=False)
            line.add_keyword("implements")
            self._generate_tokens_for_collection(self.implements, line)

        # Generate token for child nodes
        if self.child_nodes:
            self._generate_child_tokens()
            # First blank line for end of last child context and second for end of class context
            related_to_line = [self.children[-1].related_to_line, self.namespace_id]
        else:
            # If no children, both blank lines should be end of class context
            related_to_line = self.namespace_id

        self.children.set_blank_lines(
            2,
            last_is_context_end_line=True,
            related_to_line=related_to_line,
        )

        line.add_children(self.children)
        review_lines.append(line)

    def _generate_child_tokens(self):
        # Add members and methods
        for e in [p for p in self.child_nodes if not isinstance(p, FunctionNode)]:
            e.generate_tokens(self.children)

        self.children.set_blank_lines(related_to_line=self.namespace_id)
        for func in [
            x
            for x in self.child_nodes
            if isinstance(x, FunctionNode) and x.hidden == False
        ]:
            func.generate_tokens(self.children)

    def _generate_tokens_for_collection(self, values, line, *, has_suffix_space=True):
        # Helper method to concatenate list of values and generate tokens
        list_len = len(values)
        for idx, value in enumerate(values):
            line.add_type(
                value, apiview=self.apiview, has_suffix_space=has_suffix_space
            )
            # Add punctuation between types
            if idx < list_len - 1:
                line.add_punctuation(",")
