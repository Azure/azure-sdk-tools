import inspect
from ._base_node import NodeEntityBase


class VariableNode(NodeEntityBase):
    """Variable node represents class and instance variable defined in a class
    """

    def __init__(self, *, namespace, parent_node, name, type_name, value, is_ivar, dataclass_properties=None):
        super().__init__(namespace, parent_node, type_name)
        self.name = name
        self.type = type_name
        self.is_ivar = is_ivar
        self.namespace_id = "{0}.{1}({2})".format(
            self.parent_node.namespace_id, self.name, self.type
        )
        self.value = value
        self.dataclass_properties = dataclass_properties

    def generate_tokens(self, apiview):
        """Generates token for the node
        :param ApiView apiview: apiview
        """
        apiview.add_keyword("ivar" if self.is_ivar else "cvar", False, True)
        apiview.add_line_marker(self.namespace_id)
        apiview.add_text(self.name)
        # Add type
        if self.type:
            apiview.add_punctuation(":", False, True)
            apiview.add_type(self.type)

        if not self.value:
            return

        apiview.add_punctuation("=", True, True)
        if not self.dataclass_properties:
            if self.type in ["str", "Optional[str]"]:
                apiview.add_string_literal(self.value)
            else:
                apiview.add_literal(self.value)
        else:
            apiview.add_text("field")
            apiview.add_punctuation("(")
            properties = self.dataclass_properties
            for (i, property) in enumerate(properties):
                func_id = f"{self.namespace_id}.field("
                property.generate_tokens(apiview, func_id, add_line_marker=False)
                if i < len(properties) - 1:
                    apiview.add_punctuation(",", postfix_space=True)
            apiview.add_punctuation(")")
