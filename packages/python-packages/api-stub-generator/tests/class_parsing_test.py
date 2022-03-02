# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from tkinter import Variable
from apistub.nodes import ClassNode, KeyNode, VariableNode, FunctionNode
from apistubgentest.models import (
    FakeTypedDict as FakeTypedDict,
    FakeObject as FakeObject,
    PublicPrivateClass as PublicPrivateClass,
    RequiredKwargObject
)


class TestClassParsing:
    
    pkg_namespace = "apistubgentest.models._models"

    def _check_nodes(self, nodes, checks):
        assert len(nodes) == len(checks)
        for (i, node) in enumerate(nodes):
            (check_class, check_name, check_type) = checks[i]
            assert isinstance(node, check_class)
            actual_name = node.name
            assert actual_name == check_name
            if not check_class == FunctionNode:
                actual_type = node.type
                assert actual_type == check_type


    def test_typed_dict_class(self):
        class_node = ClassNode("test", None, FakeTypedDict, self.pkg_namespace)
        self._check_nodes(class_node.child_nodes, [
            (KeyNode, '"age"', "int"),
            (KeyNode, '"name"', "str"),
            (KeyNode, '"union"', f"Union[bool, {self.pkg_namespace}.FakeObject, PetEnum]")
        ])

    def test_object(self):
        class_node = ClassNode("test", None, FakeObject, self.pkg_namespace)
        self._check_nodes(class_node.child_nodes, [
            (VariableNode, "PUBLIC_CONST", "str"),
            (VariableNode, "age", "int"),
            (VariableNode, "name", "str"),
            (VariableNode, "union", "Union[bool, PetEnum]"),
            (FunctionNode, "__init__", None)
        ])

    def test_public_private(self):
        class_node = ClassNode("test", None, PublicPrivateClass, self.pkg_namespace)
        self._check_nodes(class_node.child_nodes, [
            (VariableNode, "public_dict", "dict"),
            (VariableNode, "public_var", "str"),
            (FunctionNode, "__init__", None),
            (FunctionNode, "public_func", None)
        ])

    def test_required_kwargs(self):
        class_node = ClassNode("test", None, RequiredKwargObject, self.pkg_namespace)
        kwargs = class_node.child_nodes[0].kw_args
        assert len(kwargs) == 3
        assert kwargs["name"].is_required == True
        assert kwargs["age"].is_required == True
        assert kwargs["other"].is_required == False
