import logging
import inspect
import enum
from enum import Enum
import types
import astroid
import operator

from ._base_node import NodeEntityBase, ArgType, get_qualified_name, get_navigation_id
from ._function_node import FunctionNode
from ._enum_node import EnumNode
from ._property_node import PropertyNode
from ._docstring_parser import Docstring
from _token import Token
from _token_kind import TokenKind

logging.getLogger().setLevel(logging.INFO)

find_props = lambda x: isinstance(x, PropertyNode)
find_instancefunc = lambda x: isinstance(x, FunctionNode) and not x.is_class_method and not x.name.startswith("__")
find_attr = lambda x: isinstance(x, ArgType)
find_enum = lambda x: isinstance(x, EnumNode)
find_classfunc = lambda x: isinstance(x, FunctionNode) and x.is_class_method
find_dunder_func = lambda x: isinstance(x, FunctionNode) and x.name.startswith("__")


class ClassNode(NodeEntityBase):
    """Class node to represent parsed class node and children
    """
    def __init__(self, namespace, parent_node, obj):
        super().__init__(namespace, parent_node, obj)            
        self.base_class_names = []
        self.namespace_id = self.generate_id()   
        self._inspect()        
        
    
    def _should_include_function(self, func_obj):
        """Method or Function member should only be included if it is defined in same package
        """
        if not (inspect.ismethod(func_obj) or inspect.isfunction(func_obj)) or inspect.isbuiltin(func_obj):
            return False
        if hasattr(func_obj, '__module__'):
            function_module = getattr(func_obj, '__module__')
            return function_module and function_module.startswith("azure.")

        return False
        
        
    def _inspect(self):
        """Inspect current class and it's members recursively
        """
        
        # get base classes
        self.base_class_names = self._get_base_classes()
        self.display_name = self._generate_display_name()
        # Is enum class
        self.is_enum = Enum in self.obj.__mro__
        # find members in node 
        for name, child_obj in inspect.getmembers(self.obj):
            if inspect.isbuiltin(child_obj):
                continue
            elif self._should_include_function(child_obj):
                # Include dunder and public methods
                if not name.startswith("_") or name.startswith("__"):
                    self.child_nodes.append(FunctionNode(self.namespace, self, child_obj)) 
            elif self.is_enum and isinstance(child_obj, self.obj):
                child_obj.__name__ = name
                self.child_nodes.append(EnumNode(self.namespace, self, child_obj))
            elif isinstance(child_obj, property):
                self.child_nodes.append(PropertyNode(self.namespace, self, child_obj))
        
        # Find any ivar from docstring
        if hasattr(self.obj, "__doc__"):
            docstring = getattr(self.obj, "__doc__")
            if docstring:
                docstring_parser = Docstring(docstring)
                try:
                    self.child_nodes.extend(docstring_parser.find_args("ivar"))
                except:
                    self.errors.append("Failed to parse instance variables from docstring for class {}".format(self.name))
                    

    def dump(self, delim):
        space = ' ' * delim
        print("\n{0}{1}".format(space, self.display_name))
        methods = [m for m in self.child_nodes if isinstance(m, FunctionNode)]
        props = [p for p in self.child_nodes if isinstance(p, PropertyNode)]
        vars = [v for v in self.child_nodes if isinstance(v, ArgType)]
        enums = [e for e in self.child_nodes if isinstance(e, EnumNode)]

        for c in enums:
            c.dump(delim + 4)

        if vars:
            print("{}Variables:".format(' ' * (delim +4)))
            for c in vars:
                c.dump(delim + 8)
        if props:
            print("{}Properties:".format(' ' * (delim +4)))
            for c in props:
                c.dump(delim + 8)

        for c in methods:
            c.dump(delim + 4)


    def _get_base_classes(self):
        """Find base classes
        """
        base_classes = []
        if hasattr(self.obj, "__bases__"):            
            for cl in [c for c in self.obj.__bases__ if c is not object]:
                if cl.__module__.startswith("azure"):
                    base_classes.append("{0}.{1}".format(cl.__module__, cl.__name__))
                else:
                    base_classes.append(cl.__name__)
        return base_classes                


    def _generate_display_name(self):
        """Generate dispaly name to dump
        """
        display_name = "class {}".format(self.name) 
        if self.base_class_names:
            display_name += "({})".format(", ".join(self.base_class_names))
        return display_name


    def generate_tokens(self, apiview):
        """Generates token for the node and it's children recursively and add it to apiview
        :param ApiView: apiview
        """
        apiview.add_whitespace()
        apiview.add_line_marker(self.namespace_id)
        apiview.add_keyword("class")
        apiview.add_space()
        apiview.add_text(self.namespace_id, self.name)
        # Add inherited base classes
        if self.base_class_names:
            apiview.add_punctuation("(")
            for index in range(len(self.base_class_names)):
                bname = self.base_class_names[index]
                apiview.add_type(bname, get_navigation_id(bname))
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
            

            for prop in filter(find_props, self.child_nodes):
                prop.generate_tokens(apiview)
                apiview.add_new_line()

            for e in filter(find_enum, self.child_nodes):
                apiview.add_whitespace()
                e.generate_tokens(apiview)
                apiview.add_new_line()

            for attr in filter(find_attr, self.child_nodes):
                attr.generate_tokens(apiview)
                apiview.add_new_line()
            
            instance_functions = list(filter(find_instancefunc, self.child_nodes))
            class_functions = list(filter(find_classfunc, self.child_nodes))
            dunder_functions = list(filter(find_dunder_func, self.child_nodes))

            for func in dunder_functions:
                func.generate_tokens(apiview)
                apiview.add_new_line()

            for func in class_functions:
                func.generate_tokens(apiview)
                apiview.add_new_line()

            for func in instance_functions:
                func.generate_tokens(apiview)
                apiview.add_new_line()
                
            apiview.end_group()
