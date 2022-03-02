import logging
import inspect
from enum import Enum
import operator

from ._base_node import NodeEntityBase, get_qualified_name
from ._function_node import FunctionNode
from ._enum_node import EnumNode
from ._key_node import KeyNode
from ._property_node import PropertyNode
from ._docstring_parser import DocstringParser
from ._variable_node import VariableNode


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
    """Class node to represent parsed class node and children
    """

    def __init__(self, namespace, parent_node, obj, pkg_root_namespace):
        super().__init__(namespace, parent_node, obj)
        self.base_class_names = []
        self.errors = []
        self.namespace_id = self.generate_id()
        self.full_name = self.namespace_id
        self.implements = []
        self.pkg_root_namespace = pkg_root_namespace
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
        if not (
            inspect.ismethod(func_obj) or inspect.isfunction(func_obj)
        ) or inspect.isbuiltin(func_obj):
            return False
        if hasattr(func_obj, "__module__"):
            function_module = getattr(func_obj, "__module__")
            return function_module and function_module.startswith(self.pkg_root_namespace)
        return False

    def _handle_class_variable(self, child_obj, name, *, type_string=None, value=None):
        # Add any public class level variables
        allowed_types = (str, int, dict, list, float, bool)
        if not isinstance(child_obj, allowed_types):
            return

        # if variable is already present in parsed list then just update the value
        var_match = [v for v in self.child_nodes if isinstance(v, VariableNode) and v.name == name]
        if var_match:
            if value:
                var_match[0].value = value
            if type_string:
                var_match[0].type = type_string
        else:
            self.child_nodes.append(
                VariableNode(
                    namespace=self.namespace,
                    parent_node=self,
                    name=name,
                    type_name=type_string,
                    value=value,
                    is_ivar=False
                )
            )

    def _inspect(self):
        # Inspect current class and it's members recursively
        logging.debug("Inspecting class {}".format(self.full_name))
        # get base classes
        self.base_class_names = self._get_base_classes()
        # Check if Enum is in Base class hierarchy
        self.is_enum = Enum in self.obj.__mro__
        # Find any ivar from docstring
        self._parse_ivars()

        is_typeddict = hasattr(self.obj, "__required_keys__") or hasattr(self.obj, "__optional_keys__")

        # find members in node
        for name, child_obj in inspect.getmembers(self.obj):
            if inspect.isbuiltin(child_obj):
                continue
            elif self._should_include_function(child_obj):
                # Include dunder and public methods
                if not name.startswith("_") or name.startswith("__"):
                    try:
                        self.child_nodes.append(
                            FunctionNode(self.namespace, self, child_obj)
                        )
                    except OSError:
                        # Don't create entries for things that don't have source
                        pass
            elif name == "__annotations__":
                for (item_name, item_type) in child_obj.items():
                    if item_name.startswith("_"):
                        continue
                    if is_typeddict and inspect.isclass(item_type) or getattr(item_type, "__module__", None) == "typing":
                        self.child_nodes.append(
                            KeyNode(self.namespace, self, item_name, item_type)
                        )
                    else:
                        type_string = get_qualified_name(item_type, self.namespace)
                        self._handle_class_variable(child_obj, item_name, type_string=type_string)

            # now that we've looked at the specific dunder properties we are
            # willing to include, anything with a leading underscore should be ignored.
            if name.startswith("_"):
                continue

            if self.is_enum and isinstance(child_obj, self.obj):
                # Enum values will be of parent instance type
                child_obj.__name__ = name
                self.child_nodes.append(
                    EnumNode(self.namespace, self, child_obj)
                )
            elif isinstance(child_obj, property):
                if not name.startswith("_"):
                    # Add instance properties
                    self.child_nodes.append(
                        PropertyNode(self.namespace, self, name, child_obj)
                    )
            else:
                self._handle_class_variable(child_obj, name, value=str(child_obj))

    def _parse_ivars(self):
        # This method will add instance variables by parsing docstring
        if not hasattr(self.obj, "__doc__"):
            return
        docstring = getattr(self.obj, "__doc__")
        if docstring:
            docstring_parser = DocstringParser(docstring)
            for key, var in docstring_parser.ivars.items():
                ivar_node = VariableNode(
                    namespace=self.namespace,
                    parent_node=self,
                    name=key,
                    type_name=var.argtype,
                    value=None,
                    is_ivar=True
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

    def _get_base_classes(self):
        # Find base classes
        base_classes = []
        if hasattr(self.obj, "__bases__"):
            for cl in [c for c in self.obj.__bases__ if c is not object]:
                # Show module level name for internal types to show any generated internal types
                if cl.__module__.startswith("azure"):
                    base_classes.append("{0}.{1}".format(cl.__module__, cl.__name__))
                else:
                    base_classes.append(cl.__name__)
        return base_classes

    def generate_tokens(self, apiview):
        """Generates token for the node and it's children recursively and add it to apiview
        :param ApiView: apiview
        """
        logging.info("Processing class {}".format(self.parent_node.namespace_id))
        # Generate class name line
        apiview.add_whitespace()
        apiview.add_line_marker(self.namespace_id)
        apiview.add_keyword("class", False, True)
        apiview.add_text(self.namespace_id, self.full_name, add_cross_language_id=True)

        # Add inherited base classes
        if self.base_class_names:
            apiview.add_punctuation("(")
            self._generate_token_for_collection(self.base_class_names, apiview)
            apiview.add_punctuation(")")
        apiview.add_punctuation(":")

        # Add any ABC implementation list
        if self.implements:
            apiview.add_keyword("implements", True, True)
            self._generate_token_for_collection(self.implements, apiview)
        apiview.add_newline()

        # Generate token for child nodes
        if self.child_nodes:
            self._generate_child_tokens(apiview)


    def _generate_child_tokens(self, apiview):
        # Add members and methods
        apiview.begin_group()
        for e in [p for p in self.child_nodes if not isinstance(p, FunctionNode)]:
            apiview.add_newline()
            apiview.add_whitespace()
            e.generate_tokens(apiview)
            apiview.add_newline()
        for func in [
            x
            for x in self.child_nodes
            if isinstance(x, FunctionNode) and x.hidden == False
        ]:
            apiview.set_blank_lines(1)
            func.generate_tokens(apiview)
        apiview.end_group()


    def _generate_token_for_collection(self, values, apiview):
        # Helper method to concatenate list of values and generate tokens
        list_len = len(values)
        for index in range(list_len):
            apiview.add_type(values[index], self.namespace_id)
            # Add punctuation between types
            if index < list_len - 1:
                apiview.add_punctuation(",", False, True)


    def print_errors(self):
        has_error = False
        # Check if atleast one error is present in child nodes
        for c in self.child_nodes:
            if hasattr(c, "errors") and c.errors:
                has_error = True
                break
        if has_error:
            print("-"*150)
            print("class {}".format(self.full_name))
            for c in self.child_nodes:
                c.print_errors()
