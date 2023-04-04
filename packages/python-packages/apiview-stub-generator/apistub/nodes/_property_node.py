import astroid
import inspect

from ._base_node import NodeEntityBase, get_qualified_name
from ._docstring_parser import DocstringParser
from ._astroid_parser import AstroidFunctionParser


class PropertyNode(NodeEntityBase):
    """Property node represents property defined in a class
    """

    def __init__(self, namespace, parent_node, name, obj):
        super().__init__(namespace, parent_node, obj)
        self.obj = obj
        self.read_only = True
        self.type = None
        self.name = name
        self._inspect()
        # Generate ID using name found by inspect
        self.namespace_id = self.generate_id()

    def _inspect(self):
        """Identify property name, type and readonly property
        """
        if getattr(self.obj, "fset", None):
            self.read_only = False

        if hasattr(self.obj, "fget"):
            # Get property type if type hint 
            node = astroid.extract_node(inspect.getsource(self.obj.fget))
            parser = AstroidFunctionParser(node, self.namespace, None)
            self.type = get_qualified_name(parser.return_type, self.namespace)

        # get type from docstring
        if hasattr(self.obj, "__doc__") and not self.type:
            docstring = getattr(self.obj, "__doc__")
            if docstring:
                docstring_parser = DocstringParser(getattr(self.obj, "__doc__"))
                try:
                    self.type = docstring_parser.type_for(self.name)
                    # Check for rtype docstring
                    if not self.type:
                        self.type = docstring_parser.ret_type
                except:
                    pass

        self.display_name = "{0}: {1}".format(self.name, self.type)
        if self.read_only:
            self.display_name += "   # Read-only"

    def generate_tokens(self, apiview):
        """Generates token for the node and it's children recursively and add it to apiview
        :param ApiView: apiview
        """
        apiview.add_keyword("property")
        apiview.add_space()
        apiview.add_line_marker(self.namespace_id)
        apiview.add_text(self.name)
        apiview.add_punctuation(":")
        apiview.add_space()
        apiview.add_type(self.type)
        if self.read_only:
            apiview.add_whitespace(count=5)
            apiview.add_literal("# Read-only")
        for err in self.pylint_errors:
            err.generate_tokens(apiview, self.namespace_id)
