import logging
import inspect
import enum
from enum import Enum
import types
import astroid

from _base_node import NodeEntityBase
from _function_node import FunctionNode
from _enum_node import EnumNode


logging.getLogger().setLevel(logging.INFO)


class ClassNode(NodeEntityBase):
    """Class node to represent parsed class node and children
    """
    def __init__(self, namespace, parent_node, obj):
        super().__init__(namespace, parent_node, obj)            
        self.base_class_names = []
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
                    

    def dump(self, delim):
        space = ' ' * delim
        print("\n{0}{1}".format(space, self.display_name))
        for c in self.child_nodes:
            c.dump(delim + 5)


    def _get_base_classes(self):
        """Find base classes
        """
        if hasattr(self.obj, "__bases__"):
            return [c.__name__ for c in self.obj.__bases__ if c is not object]
        return None                


    def _generate_display_name(self):
        """Generate dispaly name to dump
        """
        display_name = "class {}".format(self.name) 
        if self.base_class_names:
            display_name += "({})".format(", ".join(self.base_class_names))
        return display_name


    def generate_tokens(self):
        pass
