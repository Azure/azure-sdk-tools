from ._base_node import NodeEntityBase, get_qualified_name


class KeyNode(NodeEntityBase):
    """Key node represents a typed key defined in a TypedDict object"""

    def __init__(self, namespace, parent_node, name, type_data):
        super().__init__(namespace, parent_node, type_data)
        self.type = get_qualified_name(type_data, namespace)
        self.name = f'"{name}"'
        # Generate ID using name found by inspect
        self.namespace_id = self.generate_id()
        self.display_name = f"{self.name}: {self.type}"
        self.apiview = parent_node.apiview

    def check_handwritten(self):
        """Check if the key is handwritten by inheriting from parent class, since inspect.getfile
         doesn't return the original source if it's inherited from a different TypedDict subclass.

        :return: True if the parent class is handwritten, False otherwise.
        :rtype: bool
        """
        try:
            return self.parent_node.is_handwritten
        except Exception:
            # Default to False if errors
            return False

    def generate_tokens(self, review_lines):
        """Generates token for the node and it's children recursively and add it to apiview
        :param review_lines: ReviewLines
        """
        line = review_lines.create_review_line(is_handwritten=self.is_handwritten)
        line.add_line_marker(self.namespace_id)
        line.add_text(text="key")
        line.add_text(text=self.name, has_suffix_space=False)
        line.add_punctuation(":")
        line.add_type(self.type, apiview=self.apiview, has_suffix_space=False)
        review_lines.append(line)
