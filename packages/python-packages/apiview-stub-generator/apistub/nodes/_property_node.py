import astroid
import inspect

from ._base_node import NodeEntityBase, get_qualified_name
from ._docstring_parser import DocstringParser
from ._astroid_parser import AstroidFunctionParser


class PropertyNode(NodeEntityBase):
    """Property node represents property defined in a class"""

    def __init__(self, namespace, parent_node, name, obj):
        super().__init__(namespace, parent_node, obj)
        self.obj = obj
        self.read_only = True
        self.type = None
        self.name = name
        self.apiview = parent_node.apiview
        self._inspect()
        # Generate ID using name found by inspect
        self.namespace_id = self.generate_id()

    def _inspect(self):
        """Identify property name, type and readonly property"""
        if getattr(self.obj, "fset", None):
            self.read_only = False

        if hasattr(self.obj, "fget"):
            # Get property type if type hint
            node = astroid.extract_node(inspect.getsource(self.obj.fget))
            parser = AstroidFunctionParser(node, self.namespace, apiview=self.apiview, func_node=None)
            self.type = get_qualified_name(parser.return_type, self.namespace)

        # get type from docstring
        if hasattr(self.obj, "__doc__") and not self.type:
            docstring = getattr(self.obj, "__doc__")
            if docstring:
                docstring_parser = DocstringParser(getattr(self.obj, "__doc__"), apiview=self.apiview)
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

    def generate_tokens(self, review_lines):
        """Generates token for the node and it's children recursively and add it to apiview
        :param review_lines: ReviewLines
        """
        review_line = review_lines.create_review_line()
        review_line.add_keyword("property")
        review_line.add_line_marker(self.namespace_id)
        review_line.add_text(self.name, has_suffix_space=False)
        review_line.add_punctuation(":")
        review_line.add_type(self.type, apiview=self.apiview, has_suffix_space=False)
        if self.read_only:
            review_line.add_text(" " * 4, has_suffix_space=False)
            review_line.add_literal("# Read-only", has_suffix_space=False)
        for err in self.pylint_errors:
            err.generate_tokens(self.apiview, err=err, target_id=self.namespace_id)
        review_lines.append(review_line)
