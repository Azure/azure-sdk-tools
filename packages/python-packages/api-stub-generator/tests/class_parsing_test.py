# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from tkinter import Variable
from apistub.nodes import ClassNode, KeyNode, VariableNode, FunctionNode, EnumNode
from apistubgentest.models import (
    FakeTypedDict,
    FakeObject,
    ObjectWithDefaults,
    PetEnum,
    PublicPrivateClass,
    RequiredKwargObject,
    SomeAwesomelyNamedObject,
    SomethingWithDecorators,
    SomethingWithOverloads
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

    def _check_arg_nodes(self, nodes, checks):
        assert len(nodes) == len(checks)
        for (i, node) in enumerate(nodes.values()):
            (check_name, check_type, check_val) = checks[i]
            assert node.argname == check_name
            assert node.argtype == check_type
            assert node.default == check_val

    def test_typed_dict_class(self):
        class_node = ClassNode(name="FakeTypedDict", namespace="test", parent_node=None, obj=FakeTypedDict, pkg_root_namespace=self.pkg_namespace)
        self._check_nodes(class_node.child_nodes, [
            (KeyNode, '"age"', "int"),
            (KeyNode, '"name"', "str"),
            (KeyNode, '"union"', f"Union[bool, {self.pkg_namespace}.FakeObject, PetEnum]")
        ])

    def test_object(self):
        class_node = ClassNode(name="FakeObject", namespace="test", parent_node=None, obj=FakeObject, pkg_root_namespace=self.pkg_namespace)
        self._check_nodes(class_node.child_nodes, [
            (VariableNode, "PUBLIC_CONST", "str"),
            (VariableNode, "age", "int"),
            (VariableNode, "name", "str"),
            (VariableNode, "union", "Union[bool, PetEnum]"),
            (FunctionNode, "__init__", None)
        ])

    def test_public_private(self):
        class_node = ClassNode(name="PublicPrivateClass", namespace="test", parent_node=None, obj=PublicPrivateClass, pkg_root_namespace=self.pkg_namespace)
        self._check_nodes(class_node.child_nodes, [
            (VariableNode, "public_dict", "dict"),
            (VariableNode, "public_var", "str"),
            (FunctionNode, "__init__", None),
            (FunctionNode, "public_func", None)
        ])

    def test_required_kwargs(self):
        class_node = ClassNode(name="RequiredKwargObject", namespace="test", parent_node=None, obj=RequiredKwargObject, pkg_root_namespace=self.pkg_namespace)
        kwargs = class_node.child_nodes[0].kw_args
        args = class_node.child_nodes[0].args
        assert args["id"].is_required == True
        assert args["id"].default is None
        assert args["**kwargs"].argtype == "Any"

        assert len(kwargs) == 3
        assert kwargs["name"].is_required == True
        assert kwargs["age"].is_required == True
        assert kwargs["other"].is_required == False

    def test_default_values(self):
        class_node = ClassNode(name="ObjectWithDefaults", namespace="test", parent_node=None, obj=ObjectWithDefaults, pkg_root_namespace=self.pkg_namespace)
        assert len(class_node.child_nodes) == 1
        init_args = class_node.child_nodes[0].args
        assert init_args["name"].default == "Bob"
        assert init_args["age"].default == "21"
        assert init_args["is_awesome"].default == "True"
        assert init_args["pet"].default == "PetEnum.DOG"

    def test_model_aliases(self):
        class_node = ClassNode(name="SomeAwesomelyNamedObject", namespace="test", parent_node=None, obj=SomeAwesomelyNamedObject, pkg_root_namespace=self.pkg_namespace)
        assert class_node.name == "SomeAwesomelyNamedObject"

    def test_enum(self):
        class_node = ClassNode(name="PetEnum", namespace="test", parent_node=None, obj=PetEnum, pkg_root_namespace=self.pkg_namespace)
        assert len(class_node.child_nodes) == 3
        names = [x.name for x in class_node.child_nodes]
        assert names == ["CAT", "DEFAULT", "DOG"]

    def test_overloads(self):
        class_node = ClassNode(name="SomethingWithOverloads", namespace="test", parent_node=None, obj=SomethingWithOverloads, pkg_root_namespace=self.pkg_namespace)
        assert len(class_node.child_nodes) == 6

        node1 = class_node.child_nodes[0]
        assert "@overload" in node1.annotations
        assert node1.name == "double"
        self._check_arg_nodes(node1.args, [
            ("self", None, None),
            ("input", "int", "1"),
            ("*", None, None),
            ("test", "bool", "False"),
            ("**kwargs", None, None)
        ])
        assert node1.return_type == "int"

        node2 = class_node.child_nodes[1]
        assert "@overload" in node2.annotations
        assert node2.name == "double"
        self._check_arg_nodes(node2.args, [
            ("self", None, None),
            ("input", "Sequence[int]", "[1]"),
            ("*", None, None),
            ("test", "bool", "False"),
            ("**kwargs", None, None)
        ])
        assert node2.return_type == "list[int]"

        node3 = class_node.child_nodes[2]
        assert "@overload" not in node3.annotations
        assert node3.name == "double"
        self._check_arg_nodes(node3.args, [
            ("self", None, None),
            # This should not have all the weird collections annotations, but they
            # don't appear in the actual APIView.
            ("input", "int | collections.abc.Sequence[int]", None),
            ("*", None, None),
            ("test", "bool", "False"),
            ("**kwargs", None, None)
        ])
        assert node3.return_type == "int | list[int]"

        node4 = class_node.child_nodes[3]
        assert "@overload" in node4.annotations
        assert node4.name == "something"
        self._check_arg_nodes(node4.args, [
            ("self", None, None),
            ("id", "str", None),
            ("*args", None, None),
            ("**kwargs", None, None)
        ])
        assert node4.return_type == "str"

        node5 = class_node.child_nodes[4]
        assert "@overload" in node5.annotations
        assert node5.name == "something"
        self._check_arg_nodes(node5.args, [
            ("self", None, None),
            ("id", "int", None),
            ("*args", None, None),
            ("**kwargs", None, None)
        ])
        assert node5.return_type == "str"

        node6 = class_node.child_nodes[5]
        assert "@overload" not in node6.annotations
        assert node6.name == "something"
        self._check_arg_nodes(node6.args, [
            ("self", None, None),
            ("id", "int | str", None),
            ("*args", None, None),
            ("**kwargs", None, None)
        ])
        assert node5.return_type == "str"

    def test_decorators(self):
        class_node = ClassNode(name="SomethingWithDecorators", namespace="test", parent_node=None, obj=SomethingWithDecorators, pkg_root_namespace=self.pkg_namespace)
        assert len(class_node.child_nodes) == 4

        node1 = class_node.child_nodes[0]
        assert node1.annotations == ["@another_decorator('Test')"]

        node2 = class_node.child_nodes[1]
        assert node2.annotations == ["@another_decorator('Test')"]

        node3 = class_node.child_nodes[2]
        assert node3.annotations == ["@my_decorator"]

        node4 = class_node.child_nodes[3]
        assert node4.annotations == ["@my_decorator"]
