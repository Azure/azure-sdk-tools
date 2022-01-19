# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from apistub.nodes import ClassNode, KeyNode, VariableNode, FunctionNode

from typing import TypedDict, Union

FakeTypedDict = TypedDict(
    'FakeTypedDict',
    name=str,
    age=int,
    union=Union[bool, int]
)

class FakeObject(object):
    """ Fake Object
    :ivar str name: Name
    :ivar int age: Age
    """
    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age


class TestClassParsing:
    
    def test_typed_dict_class(self):
        class_node = ClassNode("test", None, FakeTypedDict, "test")
        assert len(class_node.child_nodes) == 3
        for node in class_node.child_nodes:
            assert isinstance(node, KeyNode)
        assert class_node.child_nodes[1].name == '"name"'
        assert class_node.child_nodes[1].type == "str"
        assert class_node.child_nodes[0].name == '"age"'
        assert class_node.child_nodes[0].type == "int"

    def test_object(self):
        class_node = ClassNode("test", None, FakeObject, "test")
        assert len(class_node.child_nodes) == 3

        node1 = class_node.child_nodes[0]
        assert isinstance(node1, VariableNode)
        assert node1.name == "age"
        assert node1.type == "int"

        node2 = class_node.child_nodes[1]
        assert isinstance(node2, VariableNode)
        assert node2.name == "name"
        assert node2.type == "str"

        node3 = class_node.child_nodes[2]
        assert isinstance(node3, FunctionNode)
        assert node3.name == "__init__"
