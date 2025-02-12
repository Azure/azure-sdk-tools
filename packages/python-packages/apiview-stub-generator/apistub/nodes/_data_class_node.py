import dataclasses
import inspect
import logging
from typing import Optional, List

from ._argtype import ArgType
from ._class_node import ClassNode
from ._variable_node import VariableNode


class DataClassNode(ClassNode):
    """Class node to represent parsed data classes"""

    def __init__(self, *, name, namespace, parent_node, obj, pkg_root_namespace, apiview):
        super().__init__(
            name=name,
            namespace=namespace,
            parent_node=parent_node,
            obj=obj,
            pkg_root_namespace=pkg_root_namespace,
            apiview=apiview,
        )
        self.decorators = [x for x in self.decorators if not x.startswith("@dataclass")]
        # explicitly set synthesized __init__ return type to None to fix test flakiness
        for child in self.child_nodes:
            if child.display_name == "__init__":
                child.return_type = None
        self.dataclass_params = self._extract_properties(getattr(obj, "__dataclass_params__", None))
        self._allow_list = [f"__{x.argname}__" for x in self.dataclass_params if x.default == True]

        # while dataclass properties looks like class variables, they are
        # actually instance variables
        dataclass_fields = getattr(obj, "__dataclass_fields__", None) or {}
        for name, properties in dataclass_fields.items():
            # convert the cvar to ivar
            var_match = [v for v in self.child_nodes if isinstance(v, VariableNode) and v.name == name]
            if var_match:
                match = var_match[0]
                match.is_ivar = True
                match.dataclass_properties = self._extract_properties(properties)

    """ Extract dataclass properties.
    
    :param class params: An object containing dataclass members.
    :keyword bool filter: If true, will filter out members with leading _ except those in the allow list. Will also strip any MISSING values.
    :keyword list allow_list: An optional list of member names to not filter.
    """

    def _extract_properties(
        self, params, *, filter: bool = True, allow_list: Optional[List[str]] = None
    ) -> List[ArgType]:
        all_props = [
            ArgType(name, argtype=None, default=obj, apiview=self.apiview, keyword=None)
            for (name, obj) in inspect.getmembers(params)
        ]
        if filter:
            allow_list = allow_list or []
            filtered = [x for x in all_props if not x.argname.startswith("_") and x.argname not in allow_list]
            filtered = [x for x in filtered if x.default != dataclasses.MISSING]
            return filtered
        else:
            return all_props

    def _generate_dataclass_annotation_properties(self, review_line, namespace):
        if self.dataclass_params:
            review_line.add_punctuation("(", has_suffix_space=False)
            for i, param in enumerate(self.dataclass_params):
                function_id = f"{self.namespace_id}.field[{param.argname}]("
                param.generate_tokens(function_id, namespace, review_line, add_line_marker=False)
                if i != len(self.dataclass_params) - 1:
                    review_line.add_punctuation(",")
            review_line.add_punctuation(")", has_suffix_space=False)

    def generate_tokens(self, review_lines):
        """Generates token for the node and it's children recursively and add it to apiview
        :param review_lines: ReviewLines
        """
        logging.info(f"Processing dataclass {self.namespace_id}")

        # Generate class name line
        review_line = review_lines.create_review_line()
        review_line.add_keyword("@dataclass", has_suffix_space=False)
        review_line.add_line_marker(f"{self.namespace_id}.@dataclass")
        self._generate_dataclass_annotation_properties(review_line, self.namespace)
        review_lines.append(review_line)
        super().generate_tokens(review_lines)
