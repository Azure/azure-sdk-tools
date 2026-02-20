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
        self.is_ivar = is_ivar
        super().__init__(namespace, parent_node, type_name, name=name)
        self.type = type_name
        self.value = value
        self.namespace_id = "{0}.{1}({2})".format(
            self.parent_node.namespace_id, self.name, self.type
        )
        self.dataclass_properties = dataclass_properties
        self.apiview = parent_node.apiview

    def check_handwritten(self):
        """Check if the variable is handwritten based on its type and inheritance.

        1. Class variables - inherit from parent class
        2. Instance variables - check if inherited from parent classes

        :return: True if the variable is handwritten, False otherwise.
        :rtype: bool
        """
        try:
            # Case 1: class variables - inherit from parent
            if not self.is_ivar:  # This is a class variable (cvar)
                if self.parent_node and hasattr(self.parent_node, 'is_handwritten'):
                    return self.parent_node.is_handwritten
                return False

            # Case 2: Instance variables - check for inheritance
            if self.is_ivar and self.parent_node:
                # Check if this variable is inherited from parent classes
                inherited_from_class = self._find_variable_source_class()

                if inherited_from_class:
                    # Variable is inherited - check the source class's file
                    try:
                        source_file = inspect.getfile(inherited_from_class)
                        return source_file.endswith("_patch.py")
                    except (TypeError, OSError):
                        return False
                else:
                    # Variable is not inherited - use parent class status
                    if hasattr(self.parent_node, 'is_handwritten'):
                        return self.parent_node.is_handwritten
                    return False

            return False
        except Exception:
            # Default to False if errors
            return False

    def _find_variable_source_class(self):
        """Find the class where this instance variable was originally defined.

        :return: The class where the variable was defined, or None if defined locally
        """
        try:
            if not self.parent_node or not hasattr(self.parent_node, 'obj'):
                return None

            current_class = self.parent_node.obj
            variable_name = self.name

            # Get the Method Resolution Order (MRO) to check parent classes
            mro = inspect.getmro(current_class)

            # Skip the current class (index 0) and check parent classes
            for parent_class in mro[1:]:
                # Check if variable exists in parent class annotations
                if hasattr(parent_class, '__annotations__') and variable_name in parent_class.__annotations__:
                    return parent_class

                # Check if variable exists as class attribute
                if hasattr(parent_class, variable_name):
                    # Make sure it's not a method or property
                    attr = getattr(parent_class, variable_name)
                    if not (inspect.ismethod(attr) or inspect.isfunction(attr) or isinstance(attr, property)):
                        return parent_class

                # Check docstring for ivar definitions (for docstring-parsed variables)
                if hasattr(parent_class, '__doc__') and parent_class.__doc__:
                    if f":ivar {variable_name}:" in parent_class.__doc__ or f":param {variable_name}:" in parent_class.__doc__:
                        return parent_class

        except Exception:
            # Default to False if errors
            pass

        # Variable not found in parent classes - it's defined locally
        return None

    def generate_tokens(self, review_lines):
        """Generates token for the node
        :param ReviewLines review_lines: ReviewLines
        """

        review_line = review_lines.create_review_line(is_handwritten=self.is_handwritten)
        review_line.add_line_marker(self.namespace_id)
        review_line.add_text(self.name, has_suffix_space=False)
        # Add type
        if self.type:
            review_line.add_punctuation(":")
            review_line.add_type(
                self.type, apiview=self.apiview, has_suffix_space=False
            )

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
                property.generate_tokens(
                    func_id, self.namespace, review_line, add_line_marker=False
                )
                if i < len(properties) - 1:
                    review_line.add_punctuation(",")
            review_line.add_punctuation(")", has_suffix_space=False)
        review_lines.append(review_line)
