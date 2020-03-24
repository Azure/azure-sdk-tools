import logging
import inspect
import ast
import io
import importlib
import operator

from ._base_node import NodeEntityBase
from ._class_node import ClassNode
from ._function_node import FunctionNode
from apistub import Navigation, Kind, NavigationTag


logging.getLogger().setLevel(logging.INFO)

filter_function = lambda x: isinstance(x, FunctionNode)
filter_class = lambda x: isinstance(x, ClassNode)

class ModuleNode(NodeEntityBase):
    """ModuleNode represents module level node and all it's children
    """
    def __init__(self, namespace, module, nodeindex):
        super().__init__(namespace, None, module)
        self.namespace_id = self.generate_id()
        self.nodeindex = nodeindex
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
                class_node = ClassNode(self.namespace, self, member_obj)
                key = "{0}.{1}".format(self.namespace, class_node.name)
                self.nodeindex.add(key, class_node)
                self.child_nodes.append(class_node)
            if inspect.ismethod(member_obj) or inspect.isfunction(member_obj):
                func_node = FunctionNode(self.namespace,  self, member_obj, True)
                key = "{0}.{1}".format(self.namespace, func_node.name)
                self.nodeindex.add(key, func_node)
                self.child_nodes.append(func_node)

        # sort classes and functions
        self.child_nodes.sort(key=operator.attrgetter('name'))

            

    def generate_tokens(self, apiview):
        """Generates token for the node and it's children recursively and add it to apiview
        :param ApiView: apiview
        """
        if self.child_nodes:
            # Add name space level functions first
            for c in filter(filter_function, self.child_nodes):                
                c.generate_tokens(apiview)
                apiview.add_new_line(2)

            # Add classes
            for c in filter(filter_class, self.child_nodes):
                c.generate_tokens(apiview)
                apiview.add_new_line(1)
            

    def get_navigation(self):
        """Generate navigation tree recursively by generating Navigation obejct for classes and functions in name space
        """
        if self.child_nodes:
            navigation = Navigation(self.namespace_id, self.namespace_id)
            navigation.set_tag(NavigationTag(Kind.type_module))
            # Generate child navigation for each child nodes
            for c in filter(filter_function, self.child_nodes):
                child_nav = Navigation(c.name, c.namespace_id)
                child_nav.set_tag(NavigationTag(Kind.type_method))
                navigation.add_child(child_nav)
            
            for c in filter(filter_class, self.child_nodes):
                child_nav = Navigation(c.name, c.namespace_id)
                child_nav.set_tag(NavigationTag(Kind.type_class))
                navigation.add_child(child_nav)
            return navigation
