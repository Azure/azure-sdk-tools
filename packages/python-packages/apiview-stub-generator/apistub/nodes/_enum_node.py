from typing import TYPE_CHECKING

from ._base_node import NodeEntityBase
from .._generated.treestyle.parser.models import ReviewToken as Token, TokenKind

if TYPE_CHECKING:
    from .._generated.treestyle.parser.models._patch import ReviewLine


class EnumNode(NodeEntityBase):
    """Enum node represents any Enum value"""

    def __init__(self, *, name, namespace, parent_node, obj):
        super().__init__(namespace, parent_node, obj)
        self.name = name
        self.value = obj.value
        self.namespace_id = self.generate_id()
        self.apiview = parent_node.apiview

    def is_pylint_error_owner(self, err):
        """Check if a pylint error belongs to this enum value.

        Enum value errors from pylint report the parent enum class as the obj,
        but have a column > 0 to distinguish them from class-level errors.

        :param err: The pylint error to check
        :return: True if this enum value owns the error, False otherwise
        """
        # Check if error obj matches the parent enum class name and column > 0 (indicating enum value)
        return err.obj == self.parent_node.name and err.column > 0

    def check_handwritten(self):
        """Check if the enum is handwritten by inheriting from parent class.
        Enum values inherit handwritten status from their parent enum class.

        :return: True if the parent enum class is handwritten, False otherwise.
        :rtype: bool
        """
        try:
            return self.parent_node.is_handwritten
        except Exception:
            # Default to False if errors
            return False

    def generate_tokens(self, review_lines):
        """Generates token for the node and it's children recursively and add it to apiview
        :param ApiView: apiview
        """
        line = review_lines.create_review_line(is_handwritten=self.is_handwritten)
        line.add_line_marker(self.namespace_id)
        line.add_text(self.name)
        line.add_punctuation("=")
        if isinstance(self.value, str):
            line.add_string_literal(self.value, has_suffix_space=False)
        else:
            line.add_literal(str(self.value), has_suffix_space=False)
        for err in self.pylint_errors:
            err.generate_tokens(self.apiview, target_id=self.namespace_id)
        review_lines.append(line)
