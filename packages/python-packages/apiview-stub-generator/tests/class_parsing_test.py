# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from apistub import TokenKind
from apistub.nodes import ClassNode
from apistubgentest.models import (
    AliasNewType,
    AliasUnion,
    ClassWithDecorators,
    ClassWithIvarsAndCvars,
    FakeTypedDict,
    FakeObject,
    GenericStack,
    PetEnumPy3MetaclassAlt,
    PublicPrivateClass,
    RequiredKwargObject,
    SomeAwesomelyNamedObject,
    SomeImplementationClass,
    SomethingAsyncWithOverloads,
    SomethingWithDecorators,
    SomethingWithInheritedOverloads,
    SomethingWithOverloads,
    SomethingWithProperties,
)

from pytest import fail

from ._test_util import _check, _tokenize, _merge_lines, _render_lines, _check_all


class TestClassParsing:

    pkg_namespace = "apistubgentest.models"
    
    def test_class_with_ivars_and_cvars(self):
        obj = ClassWithIvarsAndCvars
        class_node = ClassNode(name=obj.__name__, namespace=obj.__name__, parent_node=None, obj=obj, pkg_root_namespace=self.pkg_namespace)
        actuals = _render_lines(_tokenize(class_node))
        expected = [
            "class ClassWithIvarsAndCvars:",
            'ivar captain: str = "Picard"',
            "ivar damage: int",
            "cvar stats: ClassVar[Dict[str, int]] = {}"
        ]
        _check_all(actuals, expected, obj)

    def test_class_with_decorators(self):
        obj = ClassWithDecorators
        class_node = ClassNode(name=obj.__name__, namespace=obj.__name__, parent_node=None, obj=obj, pkg_root_namespace=self.pkg_namespace)
        actuals = _render_lines(_tokenize(class_node))
        expected = [
            "@add_id",
            "class ClassWithDecorators:",
            "",
            "def __init__(",
            "self, ",
            "id, ",
            "*args, ",
            "**kwargs",                        
            ")",
        ]
        _check_all(actuals, expected, obj)

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
            'ivar PUBLIC_CONST: str = "SOMETHING"',
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
            'ivar public_datetime: datetime',
            "ivar public_dict: dict = {'a': 'b'}",
            'ivar public_var: str = "SOMEVAL"'
        ]
        _check_all(actuals, expected, obj)
        assert actuals[5].lstrip() == "def __init__(self)"
        assert actuals[7].lstrip() == "def public_func(self, **kwargs) -> str"
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

    def test_inherited_overloads(self):
        obj = SomethingWithInheritedOverloads
        class_node = ClassNode(name=obj.__name__, namespace=obj.__name__, parent_node=None, obj=obj, pkg_root_namespace=self.pkg_namespace)
        lines = _render_lines(_tokenize(class_node))
        assert lines[2].lstrip() == "@overload"
        actual1 = lines[3]
        expected1 = 'def do_thing(val: str) -> str'
        _check(actual1, expected1, SomethingWithInheritedOverloads)

        assert lines[5].lstrip() == "@overload"
        actual2 = lines[6]
        expected2 = 'def do_thing(val: int) -> int'
        _check(actual2, expected2, SomethingWithInheritedOverloads)

        assert lines[8].lstrip() == "@overload"
        actual3 = lines[9]
        expected3 = 'def do_thing(val: bool) -> bool'
        _check(actual3, expected3, SomethingWithInheritedOverloads)

        actual4 = lines[11]
        expected4 = 'def do_thing(val: str | int | bool) -> str | int | bool'
        _check(actual4, expected4, SomethingWithInheritedOverloads)

    
    def test_overload_definition_ids(self):
        obj = SomethingWithOverloads
        obj2 = SomethingAsyncWithOverloads
        sync_class_node = ClassNode(name=obj.__name__, namespace=obj.__name__, parent_node=None, obj=obj, pkg_root_namespace=self.pkg_namespace)
        async_class_node = ClassNode(name=obj2.__name__, namespace=obj2.__name__, parent_node=None, obj=obj2, pkg_root_namespace=self.pkg_namespace)
        tokens1 = _tokenize(sync_class_node)
        tokens2 = _tokenize(async_class_node)
        self._validate_definition_ids(tokens1 + tokens2)

    def test_async_definition_ids(self):
        obj = SomethingAsyncWithOverloads
        class_node = ClassNode(name=obj.__name__, namespace=obj.__name__, parent_node=None, obj=obj, pkg_root_namespace=self.pkg_namespace)
        tokens = _tokenize(class_node)
        definition_ids = [x.definition_id for x in tokens if x.definition_id][1:]
        for def_id in definition_ids:
            assert ":async" in def_id

    # Validates that there are no repeat defintion IDs and that each line has only one definition ID.
    def _validate_definition_ids(self, tokens):
        definition_ids = set()
        def_ids_per_line = [[]]
        index = 0
        for token in tokens:
            # ensure that there are no repeated definition IDs.
            if token.definition_id:
                if token.definition_id in definition_ids:
                    fail(f"Duplicate defintion ID {token.definition_id}.")
                definition_ids.add(token.definition_id)
            # Collect the definition IDs that exist on each line
            if token.definition_id:
                def_ids_per_line[index].append(token.definition_id)
            if token.kind == TokenKind.Newline:
                index += 1
                def_ids_per_line.append([])
        # ensure that each line has either 0 or 1 definition ID.
        failures = [row for row in def_ids_per_line if len(row) > 1]
        if failures:
            fail(f"Some lines have more than one definition ID. {failures}")



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

    def test_new_type_alias(self):
        obj = AliasNewType
        class_node = ClassNode(name=obj.__name__, namespace=obj.__name__, parent_node=None, obj=obj, pkg_root_namespace=self.pkg_namespace)
        actuals = _render_lines(_tokenize(class_node))
        expected = [
            "class AliasNewType(Dict[str, str]):"
        ]
        _check_all(actuals, expected, obj)        

    def test_union_alias(self):
        obj = AliasUnion
        class_node = ClassNode(name=obj.__name__, namespace=obj.__name__, parent_node=None, obj=obj, pkg_root_namespace=self.pkg_namespace)
        actuals = _render_lines(_tokenize(class_node))
        expected = [
            "class AliasUnion(Union[str, int, bool]):"
        ]
        _check_all(actuals, expected, obj)        
