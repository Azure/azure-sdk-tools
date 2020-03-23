import logging
import re
from inspect import Parameter

from ._base_node import NodeEntityBase

logging.getLogger().setLevel(logging.INFO)



class VariableNode(NodeEntityBase):
    """Variable node represents class and instance variable defined in a class
    """
    def __init__(self, namespace, parent_node, name, type_name, is_ivar):
        super().__init__(namespace, parent_node, type_name)
        self.name = name
        self.type = type_name
        self.is_ivar = is_ivar
        self.namespace_id = "{0}.{1}({2})".format(self.parent_node.namespace_id, self.name, self.type)
        self.display_name = "{0}: {1}".format(self.name, self.type)


    def dump(self, delim):
        space = ' ' * delim
        print("{0}{1}".format(space, self.display_name))


    def generate_tokens(self, apiview):
        """Generates token for the node
        :param ApiView: apiview
        """        
        apiview.add_keyword("ivar" if self.is_ivar else "cvar")
        apiview.add_space()
        apiview.add_line_marker(self.namespace_id)
        apiview.add_text(self.namespace_id, self.name)
        apiview.add_punctuation(":")
        apiview.add_space()
        apiview.add_type(self.type)

