from ._base_node import NodeEntityBase, get_qualified_name

class KeyNode(NodeEntityBase):
    """Key node represents a typed key defined in a TypedDict object
    """

    def __init__(self, namespace, parent_node, name, type_data):
        super().__init__(namespace, parent_node, type_data)
        self.type = get_qualified_name(type_data, namespace)
        self.errors = []
        self.name = f'"{name}"'
        # Generate ID using name found by inspect
        self.namespace_id = self.generate_id()
        self.display_name = f"{self.name}: {self.type}"

    def generate_tokens(self, apiview):
        """Generates token for the node and it's children recursively and add it to apiview
        :param ApiView: apiview
        """
        apiview.add_text(None, "key")
        apiview.add_space()
        apiview.add_line_marker(self.namespace_id)
        apiview.add_text("", self.name)
        apiview.add_punctuation(":")
        apiview.add_space()
        apiview.add_type(self.type)  # TODO: Pass navigation ID if it is internal type        

    def print_errors(self):
        if self.errors:
            print("property: {}".format(self.name))
            for e in self.errors:
                print("    {}".format(e))