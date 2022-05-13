# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from apistub.nodes import ClassNode
from apistubgentest.models import (
    FakeTypedDict,
    FakeObject,
    GenericStack,
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


def _check_all(actual, expect, obj):
    for (idx, exp) in enumerate(expect):
        act = actual[idx]
        _check(act.lstrip(), exp, obj)


class TestClassParsing:

    pkg_namespace = "apistubgentest.models._models"
    
    def test_typed_dict_class(self):
        obj = FakeTypedDict
        class_node = ClassNode(name=obj.__name__, namespace=obj.__name__, parent_node=None, obj=obj, pkg_root_namespace=self.pkg_namespace)
        actuals = _render_lines(_tokenize(class_node))
        expected = [
            "class FakeTypedDict(dict):",
            'key "age": int',
            'key "name": str',
            'key "union": Union[bool, FakeObject, PetEnumPy3MetaclassAlt]'
        ]
        _check_all(actuals, expected, obj)

    def test_object(self):
        obj = FakeObject
        class_node = ClassNode(name=obj.__name__, namespace=obj.__name__, parent_node=None, obj=obj, pkg_root_namespace=self.pkg_namespace)
        actuals = _render_lines(_tokenize(class_node))
        expected = [
            "class FakeObject:",
            'cvar PUBLIC_CONST: str = "SOMETHING"',
            'ivar age: int',
            'ivar name: str',
            'ivar union: Union[bool, PetEnumPy3MetaclassAlt]'
        ]
        _check_all(actuals, expected, obj)
        init_string = _merge_lines(actuals[6:])
        assert init_string == "def __init__(self, name: str, age: int, union: Union[bool, PetEnumPy3MetaclassAlt])"

    def test_public_private(self):
        obj = PublicPrivateClass
        class_node = ClassNode(name=obj.__name__, namespace=obj.__name__, parent_node=None, obj=obj, pkg_root_namespace=self.pkg_namespace)
        actuals = _render_lines(_tokenize(class_node))
        expected = [
            "class PublicPrivateClass:",
            "cvar public_dict: dict = {'a': 'b'}",
            'cvar public_var: str = "SOMEVAL"',
        ]
        _check_all(actuals, expected, obj)
        assert actuals[4].lstrip() == "def __init__(self)"
        assert actuals[6].lstrip() == "def public_func(self, **kwargs) -> str"

    def test_required_kwargs(self):
        obj = RequiredKwargObject
        class_node = ClassNode(name=obj.__name__, namespace=obj.__name__, parent_node=None, obj=obj, pkg_root_namespace=self.pkg_namespace)
        actuals = _render_lines(_tokenize(class_node))
        init_string = _merge_lines(actuals[2:])
        assert init_string == "def __init__(self, id: str, *, age: int, name: str, other: str = ..., **kwargs: Any)"

    def test_model_aliases(self):
        obj = SomeAwesomelyNamedObject
        class_node = ClassNode(name=obj.__name__, namespace=obj.__name__, parent_node=None, obj=obj, pkg_root_namespace=self.pkg_namespace)
        lines = _render_lines(_tokenize(class_node))
        assert lines[0].lstrip() == "class SomeAwesomelyNamedObject(SomePoorlyNamedObject):"

    def test_enum(self):
        obj = PetEnumPy3MetaclassAlt
        class_node = ClassNode(name=obj.__name__, namespace=obj.__name__, parent_node=None, obj=obj, pkg_root_namespace=self.pkg_namespace)
        actuals = _render_lines(_tokenize(class_node))
        expected = [
            "class PetEnumPy3MetaclassAlt(str, Enum):",
            'CAT = "cat"',
            'DEFAULT = "cat"',
            'DOG = "dog"'
        ]
        _check_all(actuals, expected, obj)

    def test_overloads(self):
        obj = SomethingWithOverloads
        class_node = ClassNode(name=obj.__name__, namespace=obj.__name__, parent_node=None, obj=obj, pkg_root_namespace=self.pkg_namespace)
        lines = _render_lines(_tokenize(class_node))
        assert lines[2].lstrip() == "@overload"
        actual1 = _merge_lines(lines[3:10])
        expected1 = 'def double(self, input: int = 1, *, test: bool = False, **kwargs) -> int'
        _check(actual1, expected1, SomethingWithOverloads)

        assert lines[11].lstrip() == "@overload"
        actual2 = _merge_lines(lines[12:19])
        expected2 = 'def double(self, input: Sequence[int] = [1], *, test: bool = False, **kwargs) -> list[int]'
        _check(actual2, expected2, SomethingWithOverloads)

        actual3 = _merge_lines(lines[20:27])
        expected3 = 'def double(self, input: int | Sequence[int], *, test: bool = False, **kwargs) -> int | list[int]'
        _check(actual3, expected3, SomethingWithOverloads)

        assert lines[28].lstrip() == "@overload"
        actual4 = _merge_lines(lines[29:35])
        expected4 = 'def something(self, id: str, *args, **kwargs) -> str'
        _check(actual4, expected4, SomethingWithOverloads)

        assert lines[36].lstrip() == "@overload"
        actual5 = _merge_lines(lines[37:43])
        expected5 = 'def something(self, id: int, *args, **kwargs) -> str'
        _check(actual5, expected5, SomethingWithOverloads)

        actual6 = _merge_lines(lines[44:])
        expected6 = 'def something(self, id: int | str, *args, **kwargs) -> str'
        _check(actual6, expected6, SomethingWithOverloads)

    def test_decorators(self):
        obj = SomethingWithDecorators
        class_node = ClassNode(name=obj.__name__, namespace=obj.__name__, parent_node=None, obj=obj, pkg_root_namespace=self.pkg_namespace)
        actuals = _render_lines(_tokenize(class_node))

        assert actuals[2].lstrip() == "@another_decorator('Test')"
        assert actuals[5].lstrip() == "@another_decorator('Test')"
        assert actuals[8].lstrip() == "@my_decorator"
        assert actuals[11].lstrip() == "@my_decorator"

    def test_properties(self):
        obj = SomethingWithProperties
        class_node = ClassNode(name=obj.__name__, namespace=obj.__name__, parent_node=None, obj=obj, pkg_root_namespace=self.pkg_namespace)
        actuals = _render_lines(_tokenize(class_node))
        expected = [
            "class SomethingWithProperties:",
            "property docstring_property: Optional[str]    # Read-only",
            "property py2_property: Optional[str]    # Read-only",
            "property py3_property: Optional[str]    # Read-only"    
        ]
        _check_all(actuals, expected, obj)

    def test_abstract_class(self):
        obj = SomeImplementationClass
        class_node = ClassNode(name=obj.__name__, namespace=obj.__name__, parent_node=None, obj=obj, pkg_root_namespace=self.pkg_namespace)
        actuals = _render_lines(_tokenize(class_node))
        expected = [
            "class SomeImplementationClass(_SomeAbstractBase):",
            "",
            "def say_hello(self) -> str"
        ]
        for (idx, actual) in enumerate(actuals):
            expect = expected[idx]
            _check(actual, expect, SomethingWithProperties)
        
    def test_generic_class(self):
        obj = GenericStack
        class_node = ClassNode(name=obj.__name__, namespace=obj.__name__, parent_node=None, obj=obj, pkg_root_namespace=self.pkg_namespace)
        actuals = _render_lines(_tokenize(class_node))
        expected = [
            "class GenericStack(Generic[T]):"
        ]
        _check_all(actuals, expected, obj)        
