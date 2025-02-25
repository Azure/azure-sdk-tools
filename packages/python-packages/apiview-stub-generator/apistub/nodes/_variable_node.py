import inspect
from ._base_node import NodeEntityBase


class VariableNode(NodeEntityBase):
    """Variable node represents class and instance variable defined in a class"""

    def __init__(
        self,
        *,
        namespace,
        parent_node,
        name,
        type_name,
        value,
        is_ivar,
        dataclass_properties=None,
    ):
        super().__init__(namespace, parent_node, type_name)
        self.name = name
        self.type = type_name
        self.is_ivar = is_ivar
        self.namespace_id = "{0}.{1}({2})".format(self.parent_node.namespace_id, self.name, self.type)
        self.value = value
        self.dataclass_properties = dataclass_properties
        self.apiview = parent_node.apiview

    def generate_tokens(self, review_lines):
        """Generates token for the node
        :param ReviewLines review_lines: ReviewLines
        """

        var_keyword = "ivar" if self.is_ivar else "cvar"
        review_line = review_lines.create_review_line()
        review_line.add_line_marker(self.namespace_id)
        review_line.add_keyword(var_keyword)
        review_line.add_text(self.name, has_suffix_space=False)
        # Add type
        if self.type:
            review_line.add_punctuation(":")
            review_line.add_type(self.type, apiview=self.apiview, has_suffix_space=False)

        if not self.value:
            review_lines.append(review_line)
            return

        review_line.add_punctuation("=", has_prefix_space=True)
        if not self.dataclass_properties:
            if self.type in ["str", "Optional[str]"]:
                review_line.add_string_literal(self.value, has_suffix_space=False)
            else:
                review_line.add_literal(self.value, has_suffix_space=False)
        else:
            review_line.add_text("field", has_suffix_space=False)
            review_line.add_punctuation("(", has_suffix_space=False)
            properties = self.dataclass_properties
            for i, property in enumerate(properties):
                func_id = f"{self.namespace_id}.field("
                property.generate_tokens(func_id, self.namespace, review_line, add_line_marker=False)
                if i < len(properties) - 1:
                    review_line.add_punctuation(",")
            review_line.add_punctuation(")", has_suffix_space=False)
        review_lines.append(review_line)
