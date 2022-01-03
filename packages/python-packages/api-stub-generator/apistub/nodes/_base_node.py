from inspect import Parameter
from typing import Optional

class NodeEntityBase:
    """This is the base class for all node types
    :param str: namespace
        module name of the underlying object within this node
    :param: parant_node
        Parent of current node. For e.g. Class Node in case instance member is current node
    :param: obj
        Python object that is represented by current node. For e.g. class, function, property
    """

    def __init__(self, namespace, parent_node, obj):
        self.namespace = namespace
        self.parent_node = parent_node
        self.obj = obj
        self.name = ""
        if hasattr(obj, "__name__"):
            self.name = obj.__name__
        self.display_name = self.name
        self.child_nodes = []
        self.errors = []

    def generate_id(self):
        """Generates ID for current object using parent object's ID and name
        """
        namespace_id = self.namespace
        if self.parent_node:
            namespace_id = "{0}.{1}".format(self.parent_node.namespace_id, self.name)
        return namespace_id

    def generate_tokens(self, apiview):
        """Generates token for the node and it's children recursively and add it to apiview
        :param ApiView: apiview
        """
        if self.child_nodes:
            apiview.add_text("", self.display_name)
            apiview.begin_group()
            for c in self.child_nodes:
                apiview.add_new_line()
                c.generate_tokens(apiview)
            apiview.end_group()


def get_qualified_name(obj, namespace):
    """Generate and return fully qualified name of object with module name for internal types.
       If module name is not available for the object then it will return name
    :param: obj
        Parameter object of type class, function or enum
    """
    if obj is Parameter.empty:
        return None

    name = str(obj)
    if hasattr(obj, "__name__"):
        name = getattr(obj, "__name__")
        # workaround because typing.Optional __name__ is just Optional in Python 3.10
        # but not in previous versions
        if name == "Optional":
            name = str(obj)
    elif hasattr(obj, "__qualname__"):
        name = getattr(obj, "__qualname__")

    module_name = ""
    if hasattr(obj, "__module__"):
        module_name = getattr(obj, "__module__")
    if module_name and module_name.startswith(namespace):
        return "{0}.{1}".format(module_name, name)

    return name
