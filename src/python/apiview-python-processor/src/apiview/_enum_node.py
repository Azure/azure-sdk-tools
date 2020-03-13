import logging
import inspect
import astroid

from _base_node import NodeEntityBase


logging.getLogger().setLevel(logging.INFO)


class EnumNode(NodeEntityBase):
    """Enum node represents any Enum value
    """
    def __init__(self, namespace, parent_node, obj):
        super().__init__(namespace, parent_node, obj)
        self.name = obj.name
        self.value = obj.value


    def dump(self, delim):
        print("{0}{1} = {2}".format(" "* delim, self.name, self.value))


    def generate_tokens(self):
        pass