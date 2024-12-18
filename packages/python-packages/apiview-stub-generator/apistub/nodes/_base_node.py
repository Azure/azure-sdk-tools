import astroid
from inspect import Parameter
import re

from ._pylint_parser import PylintParser

keyword_regex = re.compile(r"<(class|enum) '([\w.]+)'>")
forward_ref_regex = re.compile(r"ForwardRef\('([\w.]+)'\)")
name_regex = re.compile(r"([^[]*)")


# Monkey patch NodeNG's as_string method
def as_string(self, preserve_quotes=False) -> str:
    """Get the source code that this node represents."""
    value = astroid.nodes.as_string.AsStringVisitor()(self)
    if not preserve_quotes:
        # strip any exterior quotes
        for char in ["'", '"']:
            value = value.replace(char, "")
    return value


astroid.NodeNG.as_string = as_string


class NodeEntityBase:
    """This is the base class for all node types
    :param str: namespace
        module name of the underlying object within this node
    :param: parent_node
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
        self.apiview = None
        self.pylint_errors = []
        PylintParser.match_items(obj)

    def generate_id(self):
        """Generates ID for current object using parent object's ID and name"""
        namespace_id = self.namespace
        if self.parent_node:
            namespace_id = "{0}.{1}".format(self.parent_node.namespace_id, self.name)
        return namespace_id

    def generate_tokens(self, apiview):
        """Generates token for the node and it's children recursively and add it to apiview
        :param ApiView: apiview
        """
        if self.child_nodes:
            apiview.add_text(self.display_name)
            apiview.begin_group()
            for c in self.child_nodes:
                c.generate_tokens(apiview)
            apiview.end_group()

    def generate_diagnostics(self):
        self.pylint_errors = PylintParser.get_items(self.obj)
        for child in self.child_nodes or []:
            child.generate_diagnostics()


def get_qualified_name(obj, namespace: str) -> str:
    """Generate and return fully qualified name of object with module name for internal types.
       If module name is not available for the object then it will return name
    :param: obj
        Parameter object of type class, function or enum
    """
    module_name = getattr(obj, "__module__", "")

    if module_name.startswith("astroid"):
        return obj.as_string()
    elif module_name == "types":
        return str(obj)

    if obj is Parameter.empty:
        return None

    name = str(obj)
    if hasattr(obj, "__name__"):
        name = getattr(obj, "__name__")
    elif hasattr(obj, "__qualname__"):
        name = getattr(obj, "__qualname__")

    wrap_optional = False
    args = []
    # newer versions of Python extract inner types into __args__
    # and are no longer part of the name
    if hasattr(obj, "__args__"):
        for arg in obj.__args__ or []:
            arg_string = str(arg)
            if keyword_regex.match(arg_string):
                value = keyword_regex.search(arg_string).group(2)
                if value == "NoneType":
                    # we ignore NoneType since Optional implies that NoneType is
                    # acceptable
                    if not name.startswith("Optional"):
                        wrap_optional = True
                else:
                    args.append(value)
            elif forward_ref_regex.match(arg_string):
                value = forward_ref_regex.search(arg_string).group(1)
                args.append(value)
            else:
                args.append(arg_string)

    # omit any brackets for Python 3.9/3.10 compatibility
    value = name_regex.search(name).group(0)
    if module_name and module_name.startswith(namespace):
        value = f"{module_name}.{name}"
    elif module_name and module_name != value and value.startswith(module_name):
        # strip the module name if it isn't the namespace (example: typing)
        value = value[len(module_name) + 1 :]

    if args and "[" not in value:
        arg_string = ", ".join(args)
        value = f"{value}[{arg_string}]"
    if wrap_optional:
        value = f"Optional[{value}]"
    return value
