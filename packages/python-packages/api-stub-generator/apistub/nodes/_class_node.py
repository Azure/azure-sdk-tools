import logging
import inspect
import enum
from enum import Enum
import types
import astroid
import operator

from ._base_node import NodeEntityBase
from ._function_node import FunctionNode
from ._enum_node import EnumNode
from ._property_node import PropertyNode
from ._docstring_parser import DocstringParser
from ._variable_node import VariableNode

logging.getLogger().setLevel(logging.INFO)

find_props = lambda x: isinstance(x, PropertyNode)
find_instancefunc = lambda x: isinstance(x, FunctionNode) and not x.is_class_method and not x.name.startswith("__")
find_enum = lambda x: isinstance(x, EnumNode)
find_var = lambda x: isinstance(x, VariableNode)
find_classfunc = lambda x: isinstance(x, FunctionNode) and x.is_class_method
find_dunder_func = lambda x: isinstance(x, FunctionNode) and x.name.startswith("__")


class ClassNode(NodeEntityBase):
    """Class node to represent parsed class node and children
    """
    def __init__(self, namespace, parent_node, obj):
        super().__init__(namespace, parent_node, obj)            
        self.base_class_names = []
        self.errors = []
        self.namespace_id = self.generate_id()   
        self._inspect()
        self._sort_elements()
        
    
    def _should_include_function(self, func_obj):
        # Method or Function member should only be included if it is defined in same package.
        # So this check will filter any methods defined in parent class if parent class is in non-azure package
        # for e.g. as_dict method in msrest
        if not (inspect.ismethod(func_obj) or inspect.isfunction(func_obj)) or inspect.isbuiltin(func_obj):
            return False
        if hasattr(func_obj, '__module__'):
            function_module = getattr(func_obj, '__module__')
            return function_module and function_module.startswith("azure.")

        return False
        
        
    def _inspect(self):
        """Inspect current class and it's members recursively
        """
        logging.info("Inspecting class {}".format(self.name))
        # get base classes
        self.base_class_names = self._get_base_classes()
        # Is enum class
        self.is_enum = Enum in self.obj.__mro__
         # Find any ivar from docstring
        self._parse_ivars()

        # find members in node 
        for name, child_obj in inspect.getmembers(self.obj):
            if inspect.isbuiltin(child_obj):
                continue
            elif self._should_include_function(child_obj):
                # Include dunder and public methods
                if not name.startswith("_") or name.startswith("__"):
                    self.child_nodes.append(FunctionNode(self.namespace, self, child_obj)) 
            elif self.is_enum and isinstance(child_obj, self.obj):
                # Enum values will be of parent instance type
                child_obj.__name__ = name
                self.child_nodes.append(EnumNode(self.namespace, self, child_obj))
            elif isinstance(child_obj, property):
                # Add instance properties
                self.child_nodes.append(PropertyNode(self.namespace, self, child_obj))
            elif not name.startswith("_") and (isinstance(child_obj, str) or isinstance(child_obj, int)):
                # Add any public class level variables(cvar)
                self.child_nodes.append((VariableNode(self.namespace, self, name, child_obj, False )))


    def _parse_ivars(self):
        # This method will add instance variables by parsing docstring
        if not hasattr(self.obj, "__doc__"):
            return
        docstring = getattr(self.obj, "__doc__")
        if docstring:
            docstring_parser = DocstringParser(docstring)
            for var in docstring_parser.find_args("ivar"):
                ivar_node = VariableNode(self.namespace, self, var.argname, var.argtype, True)
                self.child_nodes.append(ivar_node)


    def _sort_elements(self):
        # Sort elements in following order
        # properties, variables, Enums, dunder methods, class functions and instance methods
        # sort all elements based on name firstand then group them
        self.child_nodes.sort(key=operator.attrgetter('name'))
        sorted_children = list(filter(find_props, self.child_nodes))
        sorted_children.extend(filter(find_var, self.child_nodes))
        sorted_children.extend(filter(find_enum, self.child_nodes))
        sorted_children.extend(filter(find_dunder_func, self.child_nodes))
        sorted_children.extend(filter(find_classfunc, self.child_nodes))
        sorted_children.extend(filter(find_instancefunc, self.child_nodes))
        self.child_nodes = sorted_children


    def _get_base_classes(self):
        """Find base classes
        """
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
        # Generate class name line
        apiview.add_whitespace()
        apiview.add_line_marker(self.namespace_id)
        apiview.add_keyword("class")
        apiview.add_space()
        apiview.add_text(self.namespace_id, self.namespace_id)

        # Add inherited base classes
        if self.base_class_names:
            apiview.add_punctuation("(")
            for index in range(len(self.base_class_names)):
                apiview.add_type(self.base_class_names[index])
                # Add punctuation betwen types
                if index < len(self.base_class_names)-1:
                    apiview.add_punctuation(",")
                    apiview.add_space()
            apiview.add_punctuation(")")
        apiview.add_punctuation(":")

        # Add members and methods
        if self.child_nodes:
            apiview.add_new_line()
            apiview.begin_group()            

            for e in [p for p in self.child_nodes if not isinstance(p, FunctionNode)]:
                apiview.add_whitespace()
                e.generate_tokens(apiview)
                apiview.add_new_line()
            apiview.add_new_line(1)

            for func in [x for x in self.child_nodes if isinstance(x, FunctionNode)]:
                func.generate_tokens(apiview)
                apiview.add_new_line(1)
                
            apiview.end_group()
