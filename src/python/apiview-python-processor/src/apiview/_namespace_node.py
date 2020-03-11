import logging
import inspect
import ast
import io
import importlib

from _base_node import NodeEntityBase
from _class_node import ClassNode
from _function_node import FunctionNode


logging.getLogger().setLevel(logging.INFO)

class NameSpaceNode(NodeEntityBase):
    """NameSpaceNode represents module level node and all it's children
    """
    def __init__(self, namespace, module):
        super().__init__(module)
        self.namespace = namespace
        self._inspect()


    def _inspect(self):
        """Imports module, identify public entities in module and inspect them recursively
        """     
        public_entities = []   
        if hasattr(self.obj, "__all__"):
            public_entities = getattr(self.obj, "__all__")

        # find class and function nodes in module
        for name, member_obj in inspect.getmembers(self.obj):
            if name not in public_entities:
                continue
            if inspect.isclass(member_obj):
                self.child_nodes.append(ClassNode(member_obj))
            if inspect.ismethod(member_obj) or inspect.isfunction(member_obj):
                self.child_nodes.append(FunctionNode(member_obj))


