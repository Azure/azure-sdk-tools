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
    def __init__(self, obj):
        super().__init__(obj)            
        self.base_class_names = []
        self._inspect()
        self.display_name = "class {}".format(self.name) 
        if self.base_class_names:
            self.display_name += "({})".format(", ".join(self.base_class_names))      

    
    def _inspect(self):
        # get base classes
        self.base_class_names = self._get_base_classes()
        # Is enum class
        is_enum = Enum in self.obj.__mro__
        # find members in node 
        for name, child_obj in inspect.getmembers(self.obj):
            if inspect.isbuiltin(child_obj):
                continue
            elif (inspect.ismethod(child_obj) or inspect.isfunction(child_obj)) and not inspect.isbuiltin(child_obj):
                # Include dunder and public methods
                if not name.startswith("_") or name.startswith("__"):
                    self.child_nodes.append(FunctionNode(child_obj)) 
            elif is_enum and isinstance(child_obj, self.obj):
                child_obj.__name__ = name
                self.child_nodes.append(EnumNode(child_obj))
                    

    def dump(self, delim):
        space = ' ' * delim
        print("\n{0}{1}".format(space, self.display_name))
        for c in self.child_nodes:
            c.dump(delim + 5)


    def _get_base_classes(self):
        if hasattr(self.obj, "__bases__"):
            return [c.__name__ for c in self.obj.__bases__ if c is not object]
        return None                


    def generate_tokens(self):
        pass
