from ._base_node import NodeEntityBase, get_qualified_name
import re


class KeyNode(NodeEntityBase):
    """Key node represents a typed key defined in a TypedDict object"""

    def __init__(self, namespace, parent_node, name, type_data):
        super().__init__(namespace, parent_node, type_data)
        # Process the type to remove ForwardRef wrappers for TypedDict keys
        self.type = get_qualified_name(type_data, namespace)
        # Further process any remaining ForwardRef in the string output
        # Handle both formats: ForwardRef('Type') and ForwardRef['Type']
        self.type = re.sub(r'ForwardRef\([\'"]([\w.]+)[\'"]\)', r'\1', self.type)
        self.type = re.sub(r'ForwardRef\[[\'"]([^\'"]*)[\'"]\]', r'\1', self.type)
        self.name = f'"{name}"'
        # Generate ID using name found by inspect
        self.namespace_id = self.generate_id()
        self.display_name = f"{self.name}: {self.type}"
        self.apiview = parent_node.apiview

    def generate_tokens(self, review_lines):
        """Generates token for the node and it's children recursively and add it to apiview
        :param review_lines: ReviewLines
        """
        line = review_lines.create_review_line()
        line.add_line_marker(self.namespace_id)
        line.add_text(text="key")
        line.add_text(text=self.name, has_suffix_space=False)
        line.add_punctuation(":")
        line.add_type(self.type, apiview=self.apiview, has_suffix_space=False)
        review_lines.append(line)
