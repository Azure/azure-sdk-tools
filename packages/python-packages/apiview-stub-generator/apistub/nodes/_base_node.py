import astroid
import inspect
from inspect import Parameter
import re

from ._pylint_parser import PylintParser

keyword_regex = re.compile(r"<(class|enum) '([\w.]+)'>")
forward_ref_regex = re.compile(r"ForwardRef\('([\w.]+)'\)")
name_regex = re.compile(r"([^[]*)")
# Pattern for finding Literal expressions
literal_regex = re.compile(r'Literal\[[^\]]+\]')
# Pattern for normalizing quotes in Literal types - matches single quotes and their contents
literal_quotes_regex = re.compile(r"'([^']*)'")


def normalize_literal_quotes(value: str) -> str:
    """Normalize quotes in Literal types for consistent rendering.
    """
    # Replace single quotes with double quotes for string literals for render consistency
    return literal_quotes_regex.sub(r'"\1"', value)


def normalize_all_literals(value: str) -> str:
    """Find all Literal expressions and normalize quotes within them.
    """
    def normalize_literal_match(match):
        literal_expr = match.group(0)
        return normalize_literal_quotes(literal_expr)
    return literal_regex.sub(normalize_literal_match, value)


def process_literal_args(obj, args):
    """Unified function to handle Literal type arguments with proper quote normalization.
    """
    processed_args = []
    obj_str = str(obj)

    # Check if this is a Literal type (either direct or nested)
    is_literal = (obj_str.startswith("typing.Literal[") or
                 "Literal[" in obj_str)

    for arg in args:
        arg_string = str(arg)

        if is_literal and obj_str.startswith(("typing.Literal[")):
            # If individual string literal arg and not enum, return normalized string
            if isinstance(arg, str) and not hasattr(arg, '__module__'):
                # Plain string literal, add quotes
                arg_string = f'"{arg}"'
        elif "Literal[" in arg_string:
            # Normalize nested literal
            arg_string = normalize_all_literals(arg_string)

        processed_args.append(arg_string)

    return processed_args


# Monkey patch NodeNG's as_string method
def as_string(self, preserve_quotes=False) -> str:
    """Get the source code that this node represents."""
    value = astroid.nodes.as_string.AsStringVisitor()(self)
    # Handle Literal types - preserve quotes and normalize them
    if value.startswith("Literal["):
        return normalize_literal_quotes(value)
    # For other types, optionally strip quotes
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

    def __init__(self, namespace, parent_node, obj, *, name=""):
        self.namespace = namespace
        self.parent_node = parent_node
        self.obj = obj
        self.name = name
        if hasattr(obj, "__name__"):
            self.name = obj.__name__
        self.display_name = self.name
        self.child_nodes = []
        self.apiview = None
        self.pylint_errors = []
        self.is_handwritten = self.check_handwritten()
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

    def is_pylint_error_owner(self, err) -> bool:
        """Check if this node is the owner of a pylint error and that the error object is the same as the node.

        :param PylintError err: The pylint error to check
        :return: True if this node owns the error, False otherwise
        :rtype: bool
        """
        return err.owner == str(self.obj) and err.obj == str(self.name)

    def generate_diagnostics(self):
        self.pylint_errors = PylintParser.get_items(self)
        for child in self.child_nodes or []:
            child.generate_diagnostics()

    def check_handwritten(self):
        """Check if the object is handwritten by checking if its source file is named
            "_patch.py".

        :return: True if the object is handwritten, False if generated.
        :rtype: bool
        """
        try:
            # Unwrap the object so the function src is used, not the decorator src
            source_file = inspect.getfile(inspect.unwrap(self.obj))
            return source_file.endswith("_patch.py")
        except (TypeError, OSError):
            # inspect.getfile() can raise TypeError for built-in objects
            # or OSError if the source file cannot be found
            return False

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
        # Process all arguments with unified Literal handling
        processed_args = process_literal_args(obj, obj.__args__ or [])

        for arg_string in processed_args:
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
