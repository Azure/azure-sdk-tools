# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from collections.abc import Callable
import importlib
from types import ModuleType
from typing import Generic, TypeVar

from apistub import ApiView
from apistub.nodes import ClassNode, ModuleNode

from pytest import fail

from ._test_util import _check, _tokenize, _render_lines, MockApiView, _count_review_line_metadata


Input = TypeVar("Input")
Output = TypeVar("Output")


class GenericTask(Generic[Input, Output]):
    pass


class TestModuleParsing:

    pkg_namespace = "apiview_stub_generator_test"

    def test_parameterized_callable_alias_is_not_parsed_as_class(self):
        module = ModuleType(__name__)
        module.__all__ = ["GenericTask", "GenericTaskFactory"]
        module.GenericTask = GenericTask
        module.GenericTaskFactory = Callable[[str], GenericTask[Input, Output]]

        module_node = ModuleNode(
            namespace=module.__name__,
            module=module,
            # Reproduce the empty namespace caused by a multiline package docstring.
            pkg_root_namespace="",
            apiview=ApiView(pkg_name="test", namespace=module.__name__),
        )

        class_names = [
            node.name
            for node in module_node.child_nodes
            if isinstance(node, ClassNode)
        ]
        assert class_names == ["GenericTask"]
        assert module_node.child_nodes[0].base_class_names == ["Generic[Input, Output]"]

    # Validates that there are no repeat defintion IDs and that each line has only one definition ID.
    def _validate_line_ids(self, review_lines):
        line_ids = set()

        def collect_line_ids(review_lines, index=0):
            for line in review_lines:
                # Ensure that each line has either 0 or 1 definition ID.
                if line.line_id and not isinstance(line.line_id, str):
                    fail(f"Some lines have more than one definition ID. {line.line_id}")
                # Ensure that there are no repeated definition IDs.
                if line.line_id and line.line_id in line_ids:
                    fail(f"Duplicate definition ID {line.line_id}.")
                    line_ids.add(line.line_id)
                # Recursively collect definition IDs from child lines
                if line.children:
                    collect_line_ids(line.children, index)

        collect_line_ids(review_lines)
    def test_overloads(self):
        obj = importlib.import_module(TestModuleParsing.pkg_namespace)
        module_node = ModuleNode(
            namespace=obj.__name__,
            module=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        tokens = _tokenize(module_node)
        lines = _render_lines(tokens)
        assert lines[2].lstrip() == "@overload"
        actual1 = lines[3]
        expected1 = "def apiview_stub_generator_test.another_func(*, b: str) -> bool: ..."
        _check(actual1, expected1, obj)

        assert lines[6].lstrip() == "@overload"
        actual2 = lines[7]
        expected2 = "def apiview_stub_generator_test.another_func(*, b: int) -> bool: ..."
        _check(actual2, expected2, obj)

        assert lines[10].lstrip() == "@overload"
        actual4 = lines[11:17]
        expected4 = [
            "def apiview_stub_generator_test.module_func(",
            "    a: int, ",
            "    *, ",
            "    b: str, ",
            "    **kwargs",
            ") -> bool: ..."
        ]
        _check(actual4, expected4, obj)

        assert lines[19].lstrip() == "@overload"
        actual5 = lines[20:26]
        expected5 = [
            "def apiview_stub_generator_test.module_func(",
            "    a: int, ",
            "    *, ",
            "    b: int, ",
            "    **kwargs",
            ") -> bool: ..."
        ]
        _check(actual5, expected5, obj)

        self._validate_line_ids(tokens)
