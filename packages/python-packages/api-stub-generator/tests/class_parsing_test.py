# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from apistub.nodes import ClassNode, KeyNode, VariableNode, FunctionNode

from enum import Enum, EnumMeta
from typing import TypedDict, Union

class _CaseInsensitiveEnumMeta(EnumMeta):
    def __getitem__(self, name):
        return super().__getitem__(name.upper())

    def __getattr__(cls, name):
        """Return the enum member matching `name`
        We use __getattr__ instead of descriptors or inserting into the enum
        class' __dict__ in order to support `name` and `value` being both
        properties for enum members (which live in the class' __dict__) and
        enum members themselves.
        """
        try:
            return cls._member_map_[name.upper()]
        except KeyError:
            raise AttributeError(name)


class PetEnum(str, Enum, metaclass=_CaseInsensitiveEnumMeta):
    """A test enum
    """
    DOG = "dog"
    CAT = "cat"


class FakeObject(object):
    """ Fake Object
    :ivar str name: Name
    :ivar int age: Age
    :ivar union: Union
    :vartype union: Union[bool, PetEnum]
    """
    def __init__(self, name: str, age: int, union: Union[bool, PetEnum]):
        self.name = name
        self.age = age
        self.union = union


FakeTypedDict = TypedDict(
    'FakeTypedDict',
    name=str,
    age=int,
    union=Union[bool, FakeObject, PetEnum]
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
