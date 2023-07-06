import logging
import inspect
import astroid

from ._base_node import NodeEntityBase


class EnumNode(NodeEntityBase):
    """Enum node represents any Enum value
    """

    def __init__(self, *, name, namespace, parent_node, obj):
        super().__init__(namespace, parent_node, obj)
        self.name = name
        self.value = obj.value
        self.namespace_id = self.generate_id()

    def generate_tokens(self, apiview):
        """Generates token for the node and it's children recursively and add it to apiview
        :param ApiView: apiview
        """
        apiview.add_line_marker(self.namespace_id)
        apiview.add_text(self.name, definition_id=self.namespace_id)
        apiview.add_space()
        apiview.add_punctuation("=")
        apiview.add_space()
        if isinstance(self.value, str):
            apiview.add_string_literal(self.value)
        else:
            apiview.add_literal(str(self.value))
        for err in self.pylint_errors:
            err.generate_tokens(apiview, self.namespace_id)
