# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from apistub.nodes import ClassNode, KeyNode, VariableNode, FunctionNode
from apistubgentest.models import (
    FakeTypedDict,
    FakeObject,
    PetEnumPy3MetaclassAlt,
    PublicPrivateClass,
    RequiredKwargObject,
    SomeAwesomelyNamedObject,
    SomeImplementationClass,
    SomethingWithDecorators,
    SomethingWithOverloads,
    SomethingWithProperties
)

from ._test_util import _check, _tokenize, _merge_lines, _render_lines, _render_string


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
            (KeyNode, '"union"', f"Union[bool, {self.pkg_namespace}.FakeObject, PetEnumPy3MetaclassAlt]")
        ])

    def test_object(self):
        class_node = ClassNode(name="FakeObject", namespace="test", parent_node=None, obj=FakeObject, pkg_root_namespace=self.pkg_namespace)
        self._check_nodes(class_node.child_nodes, [
            (VariableNode, "PUBLIC_CONST", "str"),
            (VariableNode, "age", "int"),
            (VariableNode, "name", "str"),
            (VariableNode, "union", "Union[bool, PetEnumPy3MetaclassAlt]"),
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
        args = class_node.child_nodes[0].args
        kwargs = class_node.child_nodes[0].kwargs
        kwarg = class_node.child_nodes[0].special_kwarg
        assert args["id"].is_required == True
        assert args["id"].default is None
        assert kwarg.argtype == "Any"

        assert len(kwargs) == 3
        assert kwargs["name"].is_required == True
        assert kwargs["age"].is_required == True
        assert kwargs["other"].is_required == False

    def test_model_aliases(self):
        class_node = ClassNode(name="SomeAwesomelyNamedObject", namespace="test", parent_node=None, obj=SomeAwesomelyNamedObject, pkg_root_namespace=self.pkg_namespace)
        assert class_node.name == "SomeAwesomelyNamedObject"

    def test_enum(self):
        class_node = ClassNode(name="PetEnumPy3MetaclassAlt", namespace="test", parent_node=None, obj=PetEnumPy3MetaclassAlt, pkg_root_namespace=self.pkg_namespace)
        assert len(class_node.child_nodes) == 3
        names = [x.name for x in class_node.child_nodes]
        assert names == ["CAT", "DEFAULT", "DOG"]

    def test_overloads(self):
        class_node = ClassNode(name="SomethingWithOverloads", namespace="test", parent_node=None, obj=SomethingWithOverloads, pkg_root_namespace=self.pkg_namespace)
        lines = _render_lines(_tokenize(class_node))
        assert lines[2].lstrip() == "@overload"
        actual1 = _merge_lines(lines[3:10])
        expected1 = 'def double(self, input: int = 1, *, test: bool = False, **kwargs) -> int'
        _check(actual1, expected1, SomethingWithOverloads)

        assert lines[11].lstrip() == "@overload"
        actual2 = _merge_lines(lines[12:20])
        expected2 = 'def double(self, input: Sequence[int] = [1], *, test: bool = False, **kwargs) -> list[int]'
        _check(actual2, expected2, SomethingWithOverloads)

        actual3 = _merge_lines(lines[21:28])
        expected3 = 'def double(self, input: int | Sequence[int], *, test: bool = False, **kwargs) -> int | list[int]'
        _check(actual2, expected2, SomethingWithOverloads)

        assert lines[28].lstrip() == "@overload"
        actual4 = _merge_lines(lines[29:35])
        expected4 = 'def something(self, id: str, *args, **kwargs) -> str'
        _check(actual4, expected4, SomethingWithOverloads)

        assert lines[36].lstrip() == "@overload"
        actual5 = _merge_lines(lines[37:43])
        expected5 = 'def something(self, id: int, *args, **kwargs) -> str'
        _check(actual5, expected5, SomethingWithOverloads)

        actual6 = _merge_lines(lines[44:50])
        expected6 = 'def something(self, id: int | str, *args, **kwargs) -> str'
        _check(actual6, expected6, SomethingWithOverloads)

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

    def test_properties(self):
        class_node = ClassNode(name="SomethingWithProperties", namespace="test", parent_node=None, obj=SomethingWithProperties, pkg_root_namespace=self.pkg_namespace)
        actuals = [_render_string(_tokenize(x)) for x in class_node.child_nodes]
        expected = [
            "property docstring_property: Optional[str]     # Read-only",
            "property py2_property: Optional[str]     # Read-only",
            "property py3_property: Optional[str]     # Read-only"    
        ]
        for (idx, actual) in enumerate(actuals):
            expect = expected[idx]
            _check(actual, expect, SomethingWithProperties)

    def test_abstract_class(self):
        class_node = ClassNode(name="SomeImplementationClass", namespace=f"apistubgentest.models.SomeImplementationClass", parent_node=None, obj=SomeImplementationClass, pkg_root_namespace=self.pkg_namespace)
        actuals = _render_lines(_tokenize(class_node))
        expected = [
            "class apistubgentest.models.SomeImplementationClass(_SomeAbstractBase):",
            "",
            "def say_hello(self) -> str"
        ]
        for (idx, actual) in enumerate(actuals):
            expect = expected[idx]
            _check(actual, expect, SomethingWithProperties)
        