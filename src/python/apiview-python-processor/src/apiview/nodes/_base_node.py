import logging
import inspect
import astroid
from inspect import Parameter

logging.getLogger().setLevel(logging.INFO)


class ArgType:
    """Represents Arguement type
    """

    def __init__(self, name, argtype = None, default = None):
        super().__init__()
        self.argname = name
        self.argtype = argtype
        self.default = default


    def __str__(self):
        value = self.argname
        if self.argtype:
            value += ": {}".format(self.argtype)

        if self.default:
            value += " = {}".format(self.default)
        return value

    def dump(self, delim):
        space = ' ' * delim
        if self.argtype:
            print("{0}{1}: {2}".format(space, self.argname, self.argtype))
        else:
            print("{0}{1}".format(space, self.argname))

    def __repr__(self):
        return self.argname

    def is_internal_type(self):
        return  self.argtype and (self.argtype.startswith("~azure.") or self.argtype.startswith("azure."))

    def generate_tokens(self, apiview, include_default = True):
        """Generates token for the node and it's children recursively and add it to apiview
        :param ApiView: apiview
        """
        apiview.add_literal(self.argname)
        if self.argtype:
            apiview.add_punctuation(":")
            apiview.add_space()
            apiview.add_type(self.argtype, get_navigation_id(self.argtype))

        if include_default and self.default:
            apiview.add_punctuation("=")
            apiview.add_space()
            apiview.add_literal(self.default)


                       
class NodeEntityBase:

    
    def __init__(self, namespace, parent_node, obj):
        super().__init__()
        self.namespace = namespace
        self.parent_node = parent_node
        self.obj = obj
        self.name = ""
        if hasattr(obj, "__name__"):
            self.name = obj.__name__
        self.display_name = self.name        
        self.child_nodes = []
        
    
    def get_display_name(self):
        return self.display_name    


    def get_child_nodes(self):
        return child_nodes


    def get_name(self):
        return name


    def generate_id(self):
        namespace_id = self.namespace
        if self.parent_node:
            namespace_id = "{0}:{1}".format(self.parent_node.namespace_id, self.name)
        return namespace_id
        

    def dump(self, delim = 0):
        if not self.child_nodes:
            return None
        print("\n{}\n".format(self.display_name))
        for n in self.child_nodes:
            n.dump(delim+5)


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

    
def get_qualified_name(obj):
    if obj is Parameter.empty:
        return None

    name = str(obj)
    if hasattr(obj, "__name__"):
        name = getattr(obj, "__name__")
    elif hasattr(obj, "__qualname__"):
        name = getattr(obj, "__qualname__")

    module_name = ""
    if hasattr(obj, "__module__"):
        module_name = getattr(obj, "__module__")
    if module_name and module_name.startswith('azure'):
        return "{0}.{1}".format(module_name, name)

    return name


def is_internal_type(type_name):
    return type_name.startswith("azure.") or type_name.startswith("~azure.")

    
def get_navigation_id(type_name):
    """This method will return the id for a given type and this id will be used as navigation ID in tokens
    """
    return None