import logging
import inspect
import astroid

from ._base_node import NodeEntityBase


logging.getLogger().setLevel(logging.INFO)


class EnumNode(NodeEntityBase):
    """Enum node represents any Enum value
    """
    def __init__(self, namespace, parent_node, obj):
        super().__init__(namespace, parent_node, obj)
        self.name = obj.name
        self.value = obj.value
        self.namespace_id = self.generate_id()


    def dump(self, delim):
        print("{0}{1} = {2}".format(" "* delim, self.name, self.value))


    def generate_tokens(self, apiview):
        """Generates token for the node and it's children recursively and add it to apiview
        :param ApiView: apiview
        """        
        apiview.add_whitespace()
        apiview.add_keyword(self.name)
        apiview.add_space()
        apiview.add_punctuation("=")
        apiview.add_space()
        if self.value.isdigit():
            apiview.add_literal(self.value)
        else:
            apiview.add_stringliteral(self.value)
