import logging
import inspect
import ast
import io
import importlib

from ._base_node import NodeEntityBase
from ._class_node import ClassNode
from ._function_node import FunctionNode
from _apiview import ApiView


logging.getLogger().setLevel(logging.INFO)

filter_function = lambda x: isinstance(x, FunctionNode)
filter_class = lambda x: isinstance(x, ClassNode)

class NameSpaceNode(NodeEntityBase):
    """NameSpaceNode represents module level node and all it's children
    """
    def __init__(self, namespace, module):
        super().__init__(namespace, None, module)
        self.namespace_id = self.generate_id()
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
                self.child_nodes.append(ClassNode(self.namespace, self, member_obj))
            if inspect.ismethod(member_obj) or inspect.isfunction(member_obj):
                self.child_nodes.append(FunctionNode(self.namespace,  self, member_obj))

            

    def generate_tokens(self, apiview):
        """Generates token for the node and it's children recursively and add it to apiview
        :param ApiView: apiview
        """
        if self.child_nodes:
            apiview.add_line_marker(self.namespace_id)
            apiview.add_text(self.namespace_id, self.display_name)
            apiview.begin_group()
            # Add name space level functions first
            for c in filter(filter_function, self.child_nodes):
                apiview.add_new_line()
                c.generate_tokens(apiview)
            # Add classes
            for c in filter(filter_class, self.child_nodes):
                apiview.add_new_line()
                c.generate_tokens(apiview)
            apiview.end_group()
