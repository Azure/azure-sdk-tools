import logging
import re
from inspect import Parameter

from ._base_node import NodeEntityBase
from ._docstring_parser import Docstring

logging.getLogger().setLevel(logging.INFO)



class PropertyNode(NodeEntityBase):
    """Property node represents property defined in a class
    """
    def __init__(self, namespace, parent_node, obj):
        super().__init__(namespace, parent_node, obj)
        self.obj = obj
        self.read_only = True
        self.type = "(Not found)"
        self.errors = []
        self._inspect()
        # Generate ID using name found by inspect
        self.namespace_id = self.generate_id()


    def _inspect(self):
        """Identify property name, type and readonly property
        """
        self.name = getattr(self.obj, "fget").__name__
        if getattr(self.obj, "fset"):
            self.read_only = False
        
        # get type from docstring
        if hasattr(self.obj, "__doc__"):
            docstring = getattr(self.obj, "__doc__")
            # if docstring is missing then look for docstring at setter method if it is not a readonly
            if not docstring and not self.read_only and hasattr(self.obj.fset, "__doc__"):
                docstring = getattr(self.obj.fset, "__doc__")
            
            if docstring:
                docstring_parser = Docstring(getattr(self.obj, "__doc__"))
                try:
                    self.type = docstring_parser.find_type()
                except:
                    self.errors.append("Failed to find type of property {}".format(self.name))


        self.display_name = "{0}: {1}".format(self.name, self.type)
        if self.read_only:
            self.display_name += "   # Read-only"


    def dump(self, delim):
        space = ' ' * delim
        print("{0}{1}".format(space, self.display_name))


    def generate_tokens(self, apiview):
        """Generates token for the node and it's children recursively and add it to apiview
        :param ApiView: apiview
        """        
        apiview.add_keyword("property")
        apiview.add_space()
        apiview.add_line_marker(self.namespace_id)
        apiview.add_text(self.namespace_id, self.name)
        apiview.add_punctuation(":")
        apiview.add_space()
        apiview.add_type(self.type)  #todo Pass navigation ID if it is internal type
        if self.read_only:
            apiview.add_whitespace()
            apiview.add_literal("# Read-only")
