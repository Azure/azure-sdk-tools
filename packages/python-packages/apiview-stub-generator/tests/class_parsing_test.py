# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

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
    SomeProtocolDecorator,
)

from pytest import fail

from ._test_util import _check, _tokenize, _render_lines, MockApiView, _count_review_line_metadata


def _check_all(actual, expect, obj):
    for idx, exp in enumerate(expect):
        act = actual[idx]
        _check(act.lstrip(), exp, obj)


class TestClassParsing:

    pkg_namespace = "apistubgentest.models"

    def test_class_with_ivars_and_cvars(self):
        obj = ClassWithIvarsAndCvars
        class_node = ClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        tokens = _tokenize(class_node)
        actuals = _render_lines(tokens)
        expected = [
            "class ClassWithIvarsAndCvars:",
            'ivar captain: str = "Picard"',
            "ivar damage: int",
            "cvar stats: ClassVar[Dict[str, int]] = {}",
        ]
        _check_all(actuals, expected, obj)
        metadata = {"RelatedToLine": 0, "IsContextEndLine": 0}
        metadata = _count_review_line_metadata(tokens, metadata)
        assert metadata["RelatedToLine"] == 0
        assert metadata["IsContextEndLine"] == 1

    def test_class_with_decorators(self):
        obj = ClassWithDecorators
        class_node = ClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        tokens = _tokenize(class_node)
        actuals = _render_lines(tokens)
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

        metadata = {"RelatedToLine": 0, "IsContextEndLine": 0}
        metadata = _count_review_line_metadata(tokens, metadata)
        assert metadata["RelatedToLine"] == 1
        assert metadata["IsContextEndLine"] == 2

    def test_typed_dict_class(self):
        obj = FakeTypedDict
        class_node = ClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        tokens = _tokenize(class_node)
        actuals = _render_lines(tokens)
        expected = [
            "class FakeTypedDict(dict):",
            'key "age": int',
            'key "name": str',
            'key "union": Union[bool, FakeObject, PetEnumPy3MetaclassAlt]',
        ]
        _check_all(actuals, expected, obj)
        metadata = {"RelatedToLine": 0, "IsContextEndLine": 0}
        metadata = _count_review_line_metadata(tokens, metadata)
        assert metadata["RelatedToLine"] == 0
        assert metadata["IsContextEndLine"] == 1

    def test_object(self):
        obj = FakeObject
        class_node = ClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        tokens = _tokenize(class_node)
        actuals = _render_lines(tokens)
        expected = [
            "class FakeObject:",
            'ivar PUBLIC_CONST: str = "SOMETHING"',
            "ivar age: int",
            "ivar name: str",
            "ivar union: Union[bool, PetEnumPy3MetaclassAlt]",
        ]
        _check_all(actuals, expected, obj)
        expected = [
            "def __init__(",
            "    self, ",
            "    name: str, ",
            "    age: int, ",
            "    union: Union[bool, PetEnumPy3MetaclassAlt]",
            ")",
        ]
        for idx, exp in enumerate(expected):
            assert actuals[idx + 6] == exp

        metadata = {"RelatedToLine": 0, "IsContextEndLine": 0}
        metadata = _count_review_line_metadata(tokens, metadata)
        assert metadata["RelatedToLine"] == 0
        assert metadata["IsContextEndLine"] == 2

    def test_public_private(self):
        obj = PublicPrivateClass
        class_node = ClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        tokens = _tokenize(class_node)
        actuals = _render_lines(tokens)
        expected = [
            "class PublicPrivateClass:",
            "ivar public_datetime: datetime",
            "ivar public_dict: dict = {'a': 'b'}",
            'ivar public_var: str = "SOMEVAL"',
        ]
        _check_all(actuals, expected, obj)
        assert actuals[5].lstrip() == "def __init__(self)"
        assert actuals[7].lstrip() == "def public_func(self, **kwargs) -> str"

        metadata = {"RelatedToLine": 0, "IsContextEndLine": 0}
        metadata = _count_review_line_metadata(tokens, metadata)
        assert metadata["RelatedToLine"] == 0

        # no context end lines for all single line functions
        assert metadata["IsContextEndLine"] == 1

    def test_required_kwargs(self):
        obj = RequiredKwargObject
        class_node = ClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        tokens = _tokenize(class_node)
        actuals = _render_lines(tokens)
        expected = [
            "def __init__(",
            "    self, ",
            "    id: str, ",
            "    *, ",
            "    age: int, ",
            "    name: str, ",
            "    other: str = ..., ",
            "    **kwargs: Any",
            ")"
        ]
        for idx, exp in enumerate(expected):
            assert actuals[idx + 2] == exp

        metadata = {"RelatedToLine": 0, "IsContextEndLine": 0}
        metadata = _count_review_line_metadata(tokens, metadata)
        assert metadata["RelatedToLine"] == 0
        assert metadata["IsContextEndLine"] == 2

    def test_model_aliases(self):
        obj = SomeAwesomelyNamedObject
        class_node = ClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        tokens = _tokenize(class_node)
        lines = _render_lines(tokens)
        assert lines[0].lstrip() == "class SomeAwesomelyNamedObject(SomePoorlyNamedObject):"

        metadata = {"RelatedToLine": 0, "IsContextEndLine": 0}
        metadata = _count_review_line_metadata(tokens, metadata)
        assert metadata["RelatedToLine"] == 0
        assert metadata["IsContextEndLine"] == 1

    def test_enum(self):
        obj = PetEnumPy3MetaclassAlt
        class_node = ClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        tokens = _tokenize(class_node)
        actuals = _render_lines(tokens)
        expected = ["class PetEnumPy3MetaclassAlt(str, Enum):", 'CAT = "cat"', 'DEFAULT = "cat"', 'DOG = "dog"']
        _check_all(actuals, expected, obj)

        metadata = {"RelatedToLine": 0, "IsContextEndLine": 0}
        metadata = _count_review_line_metadata(tokens, metadata)
        assert metadata["RelatedToLine"] == 0
        assert metadata["IsContextEndLine"] == 1

    def test_overloads(self):
        obj = SomethingWithOverloads
        class_node = ClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        tokens = _tokenize(class_node)
        lines = _render_lines(tokens)
        assert lines[2].lstrip() == "@overload"
        actual1 = lines[3:10]
        expected1 = [
            "def double(",
            "    self, ",
            "    input: int = 1, ",
            "    *, ",
            "    test: bool = False, ",
            "    **kwargs",
            ") -> int"
        ]
        _check(actual1, expected1, SomethingWithOverloads)

        assert lines[11].lstrip() == "@overload"
        actual2 = lines[12:19]
        expected2 = [
            "def double(",
            "    self, ",
            "    input: Sequence[int] = [1], ",
            "    *, ",
            "    test: bool = False, ",
            "    **kwargs",
            ") -> list[int]"
        ]
        _check(actual2, expected2, SomethingWithOverloads)

        actual3 = lines[20:27]
        expected3 = [
            "def double(",
            "    self, ",
            "    input: int | Sequence[int], ",
            "    *, ",
            "    test: bool = False, ",
            "    **kwargs",
            ") -> int | list[int]"
        ]
        _check(actual3, expected3, SomethingWithOverloads)

        assert lines[28] == "@overload"
        actual4 = lines[29:35]
        expected4 = [
            "def something(",
            "    self, ",
            "    id: str, ",
            "    *args, ",
            "    **kwargs",
            ") -> str"
        ]
        _check(actual4, expected4, SomethingWithOverloads)

        assert lines[36] == "@overload"
        actual5 = lines[37:43]
        expected5 = [
            "def something(",
            "    self, ",
            "    id: int, ",
            "    *args, ",
            "    **kwargs",
            ") -> str"
        ]
        _check(actual5, expected5, SomethingWithOverloads)

        actual6 = lines[44:]
        expected6 = [
            "def something(",
            "    self, ",
            "    id: int | str, ",
            "    *args, ",
            "    **kwargs",
            ") -> str",
            "", # Blank line at the end of the function
            "", # Blank line at the end of the class
        ]
        _check(actual6, expected6, SomethingWithOverloads)

        # Check that the RelatedToLine and IsContextEndLine are being set correctly.
        metadata = {"RelatedToLine": 0, "IsContextEndLine": 0}
        metadata = _count_review_line_metadata(tokens, metadata)
        assert metadata["RelatedToLine"] == 4
        assert metadata["IsContextEndLine"] == 7

    def test_inherited_overloads(self):
        obj = SomethingWithInheritedOverloads
        class_node = ClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        tokens = _tokenize(class_node)
        lines = _render_lines(tokens)
        assert lines[2].lstrip() == "@overload"
        actual1 = lines[3]
        expected1 = "def do_thing(val: str) -> str"
        _check(actual1, expected1, SomethingWithInheritedOverloads)

        assert lines[5].lstrip() == "@overload"
        actual2 = lines[6]
        expected2 = "def do_thing(val: int) -> int"
        _check(actual2, expected2, SomethingWithInheritedOverloads)

        assert lines[8].lstrip() == "@overload"
        actual3 = lines[9]
        expected3 = "def do_thing(val: bool) -> bool"
        _check(actual3, expected3, SomethingWithInheritedOverloads)

        actual4 = lines[11]
        expected4 = "def do_thing(val: str | int | bool) -> str | int | bool"
        _check(actual4, expected4, SomethingWithInheritedOverloads)

        # Check that the RelatedToLine and IsContextEndLine are being set correctly.
        metadata = {"RelatedToLine": 0, "IsContextEndLine": 0}
        metadata = _count_review_line_metadata(tokens, metadata)
        assert metadata["RelatedToLine"] == 3
        # TODO: For overloads, IsContextEndLine is only set for the implementation method. Should it be per overload?
        assert metadata["IsContextEndLine"] == 1

    def test_overload_line_ids(self):
        obj = SomethingWithOverloads
        obj2 = SomethingAsyncWithOverloads
        sync_class_node = ClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        async_class_node = ClassNode(
            name=obj2.__name__,
            namespace=obj2.__name__,
            parent_node=None,
            obj=obj2,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        tokens1 = _tokenize(sync_class_node)
        tokens2 = _tokenize(async_class_node)
        self._validate_line_ids(tokens1 + tokens2)

    def test_async_line_ids(self):
        obj = SomethingAsyncWithOverloads
        class_node = ClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        tokens = _tokenize(class_node)
        line_ids = [x.line_id for x in tokens if x.line_id][1:]
        for def_id in line_ids:
            assert ":async" in def_id

        metadata = {"RelatedToLine": 0, "IsContextEndLine": 0}
        metadata = _count_review_line_metadata(tokens, metadata)
        assert metadata["RelatedToLine"] == 4
        assert metadata["IsContextEndLine"] == 7

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

    def test_decorators(self):
        obj = SomethingWithDecorators
        class_node = ClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        tokens = _tokenize(class_node)
        actuals = _render_lines(tokens)

        assert actuals[2].lstrip() == "@another_decorator('Test')"
        assert actuals[5].lstrip() == "@another_decorator('Test')"
        assert actuals[8].lstrip() == "@my_decorator"
        assert actuals[11].lstrip() == "@my_decorator"

        metadata = {"RelatedToLine": 0, "IsContextEndLine": 0}
        metadata = _count_review_line_metadata(tokens, metadata)
        assert metadata["RelatedToLine"] == 4
        # all single-line methods so no context end lines
        assert metadata["IsContextEndLine"] == 1

    def test_protocol_decorator_related_to_line(self):
        obj = SomeProtocolDecorator
        class_node = ClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        tokens = _tokenize(class_node)
        actuals = _render_lines(tokens)
        expected = [
            "@runtime_checkable",
            "class SomeProtocolDecorator(Protocol):",
        ]
        _check_all(actuals, expected, obj)

        # Check that the RelatedToLine and IsContextEndLine are being set correctly.
        metadata = {"RelatedToLine": 0, "IsContextEndLine": 0}
        metadata = _count_review_line_metadata(tokens, metadata)
        assert metadata["RelatedToLine"] == 1
        # Body of class is not defined so there is no context end line
        assert metadata["IsContextEndLine"] == 0

    def test_properties(self):
        obj = SomethingWithProperties
        class_node = ClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        tokens = _tokenize(class_node)
        actuals = _render_lines(tokens)
        expected = [
            "class SomethingWithProperties:",
            "property docstring_property: Optional[str]    # Read-only",
            "property py2_property: Optional[str]    # Read-only",
            "property py3_property: Optional[str]    # Read-only",
        ]
        _check_all(actuals, expected, obj)
        metadata = {"RelatedToLine": 0, "IsContextEndLine": 0}
        metadata = _count_review_line_metadata(tokens, metadata)
        assert metadata["RelatedToLine"] == 0
        assert metadata["IsContextEndLine"] == 1

    def test_abstract_class(self):
        obj = SomeImplementationClass
        class_node = ClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        tokens = _tokenize(class_node)
        actuals = _render_lines(tokens)
        expected = ["class SomeImplementationClass(_SomeAbstractBase):", "", "def say_hello(self) -> str", "", ""]
        for idx, actual in enumerate(actuals):
            expect = expected[idx]
            _check(actual, expect, SomethingWithProperties)

        metadata = {"RelatedToLine": 0, "IsContextEndLine": 0}
        metadata = _count_review_line_metadata(tokens, metadata)
        assert metadata["RelatedToLine"] == 0
        assert metadata["IsContextEndLine"] == 1

    def test_generic_class(self):
        obj = GenericStack
        class_node = ClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        tokens = _tokenize(class_node)
        actuals = _render_lines(tokens)
        expected = ["class GenericStack(Generic[T]):"]
        _check_all(actuals, expected, obj)

        metadata = {"RelatedToLine": 0, "IsContextEndLine": 0}
        metadata = _count_review_line_metadata(tokens, metadata)
        assert metadata["RelatedToLine"] == 0
        assert metadata["IsContextEndLine"] == 1

    def test_new_type_alias(self):
        obj = AliasNewType
        class_node = ClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        tokens = _tokenize(class_node)
        actuals = _render_lines(tokens)
        expected = ["class AliasNewType(Dict[str, str]):"]
        _check_all(actuals, expected, obj)

        metadata = {"RelatedToLine": 0, "IsContextEndLine": 0}
        metadata = _count_review_line_metadata(tokens, metadata)
        assert metadata["RelatedToLine"] == 0
        # class type with no defined body
        assert metadata["IsContextEndLine"] == 0

    def test_union_alias(self):
        obj = AliasUnion
        class_node = ClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        tokens = _tokenize(class_node)
        actuals = _render_lines(tokens)
        expected = ["class AliasUnion(Union[str, int, bool]):"]
        _check_all(actuals, expected, obj)

        metadata = {"RelatedToLine": 0, "IsContextEndLine": 0}
        metadata = _count_review_line_metadata(tokens, metadata)
        assert metadata["RelatedToLine"] == 0
        # class type with no defined body
        assert metadata["IsContextEndLine"] == 0
