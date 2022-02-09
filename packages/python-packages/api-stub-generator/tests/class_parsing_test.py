# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from apistub.nodes import ClassNode, KeyNode, VariableNode, FunctionNode
from apistubgen-tests import (
    FakeTypedDict, FakeObject
)


class TestClassParsing:
    
    def test_typed_dict_class(self):
        class_node = ClassNode("test", None, FakeTypedDict, "test")
        assert len(class_node.child_nodes) == 3
        for node in class_node.child_nodes:
            assert isinstance(node, KeyNode)

        node1 = class_node.child_nodes[0]
        assert node1.name == '"age"'
        assert node1.type == "int"

        node2 = class_node.child_nodes[1]
        assert node2.name == '"name"'
        assert node2.type == "str"

        node3 = class_node.child_nodes[2]
        assert node3.name == '"union"'
        assert node3.type == "Union[bool, tests.class_parsing_test.FakeObject, PetEnum]"


    def test_object(self):
        class_node = ClassNode("test", None, FakeObject, "test")
        assert len(class_node.child_nodes) == 4

        node1 = class_node.child_nodes[0]
        assert isinstance(node1, VariableNode)
        assert node1.name == "age"
        assert node1.type == "int"

        node2 = class_node.child_nodes[1]
        assert isinstance(node2, VariableNode)
        assert node2.name == "name"
        assert node2.type == "str"

        node3 = class_node.child_nodes[2]
        assert isinstance(node3, VariableNode)
        assert node3.name == "union"
        assert node3.type == "Union[bool, PetEnum]"

        node4 = class_node.child_nodes[3]
        assert isinstance(node4, FunctionNode)
        assert node4.name == "__init__"
