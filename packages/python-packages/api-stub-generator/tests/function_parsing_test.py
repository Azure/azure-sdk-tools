# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from apistub.nodes import FunctionNode
from apistubgentest import (
    Python2TypeHintClient,
    Python3TypeHintClient,
    DocstringTypeHintClient,
    DefaultValuesClient,
    SpecialArgsClient
)

from ._test_util import _render_string, _check, _tokenize


def _test_arg(node, key, name, _type, default):
    arg = node.args[key]
    assert arg.argname == name
    assert arg.argtype == _type
    assert arg.default == default


def _test_return(node, _type):
    assert node.return_type == _type


class TestTypeHints:

    def test_simple_typehints(self):
        clients = [Python2TypeHintClient, Python3TypeHintClient, DocstringTypeHintClient]
        for client in clients:
            node = FunctionNode("test", None, obj=client.with_simple_typehints)
            actual = _render_string(_tokenize(node))
            expected = "def with_simple_typehints(self, name: str, age: int) -> str"
            _check(actual, expected, client)

    def test_complex_typehints(self):
        clients = [Python2TypeHintClient, Python3TypeHintClient, DocstringTypeHintClient]
        for client in clients:
            node = FunctionNode("test", None, obj=client.with_complex_typehints)
            actual = _render_string(_tokenize(node))
            expected = "def with_complex_typehints(self, value: List[ItemPaged[Union[FakeObject, FakeError]]])"
            _check(actual, expected, client)

    def test_variadic_typehints(self):
        clients = [
            # TODO: Known limitation of astroid (See: https://github.com/Azure/azure-sdk-tools/issues/3131)
            #Python2TypeHintClient,
            Python3TypeHintClient,
            DocstringTypeHintClient
        ]
        for client in clients:
            node = FunctionNode("test", None, obj=client.with_variadic_typehint)
            actual = _render_string(_tokenize(node))
            expected = "def with_variadic_typehint(self, *vars: str, **kwargs: Any) -> None"
            _check(actual, expected, client)

    def test_str_list_return_type(self):
        clients = [Python2TypeHintClient, Python3TypeHintClient, DocstringTypeHintClient]
        for client in clients:
            node = FunctionNode("test", None, obj=client.with_str_list_return_type)
            actual = _render_string(_tokenize(node))
            expected = "def with_str_list_return_type(self) -> List[str]"
            _check(actual, expected, client)

    def test_list_return_type(self):
        clients = [Python2TypeHintClient, Python3TypeHintClient, DocstringTypeHintClient]
        for client in clients:
            node = FunctionNode("test", None, obj=client.with_list_return_type)
            actual = _render_string(_tokenize(node))
            expected = "def with_list_return_type(self) -> List[TestClass]"
            _check(actual, expected, client)

    def test_list_union_return_type(self):
        clients = [Python2TypeHintClient, Python3TypeHintClient, DocstringTypeHintClient]
        for client in clients:
            node = FunctionNode("test", None, obj=client.with_list_union_return_type)
            actual = _render_string(_tokenize(node))
            expected = "def with_list_union_return_type(self) -> List[Union[str, int]]"
            _check(actual, expected, client)

    def test_datetime_typehint(self):
        clients = [Python2TypeHintClient, Python3TypeHintClient, DocstringTypeHintClient]
        for client in clients:
            node = FunctionNode("test", None, obj=client.with_datetime_typehint)
            actual = _render_string(_tokenize(node))
            expected = "def with_datetime_typehint(self, date: datetime) -> datetime"
            _check(actual, expected, client)    


class TestDefaultValues:

    def test_simple_default(self):
        node = FunctionNode("test", None, obj=DefaultValuesClient.with_simple_default)
        actual = _render_string(_tokenize(node))
        expected = 'def with_simple_default(name: str = "Bill", *, age: int = 21)'
        _check(actual, expected, DefaultValuesClient)

    def test_simple_optional_default(self):
        node = FunctionNode("test", None, obj=DefaultValuesClient.with_simple_optional_defaults)
        actual = _render_string(_tokenize(node))
        expected = 'def with_simple_optional_defaults(name: Optional[str] = "Bill", *, age: Optional[int] = 21)'
        _check(actual, expected, DefaultValuesClient)

    def test_optional_none_default(self):
        node = FunctionNode("test", None, obj=DefaultValuesClient.with_optional_none_defaults)
        actual = _render_string(_tokenize(node))
        expected = 'def with_optional_none_defaults(name: Optional[str] = None, *, age: Optional[int] = ...)'
        _check(actual, expected, DefaultValuesClient)

    def test_class_default(self):
        node = FunctionNode("test", None, obj=DefaultValuesClient.with_class_default)
        actual = _render_string(_tokenize(node))
        expected = 'def with_class_default(my_class: Any = FakeObject)'
        _check(actual, expected, DefaultValuesClient)

    def test_parsed_docstring_defaults(self):
        node = FunctionNode("test", None, obj=DefaultValuesClient.with_parsed_docstring_defaults)
        actual = _render_string(_tokenize(node))
        expected = 'def with_parsed_docstring_defaults(name: str = "Bill", age: int = 21, some_class: class = ":py:class:`apistubgen.test.models.FakeObject`")'
        _check(actual, expected, DefaultValuesClient)

    def test_enum_defaults(self):
        node = FunctionNode("test", None, obj=DefaultValuesClient.with_enum_defaults)
        actual = _render_string(_tokenize(node))
        expected = 'def with_enum_defaults(enum1: Union[PetEnumPy3Metaclass, str] = "DOG", enum2: Union[PetEnumPy3Metaclass, str] = PetEnumPy3Metaclass.DOG)'
        _check(actual, expected, DefaultValuesClient)


class TestSpecialArguments:

    def test_standard_names(self):
        node = FunctionNode("test", None, obj=SpecialArgsClient.with_standard_names)
        actual = _render_string(_tokenize(node))
        expected = 'def with_standard_names(self, *args, **kwargs)'
        _check(actual, expected, SpecialArgsClient)

    def test_nonstandard_names(self):
        node = FunctionNode("test", None, obj=SpecialArgsClient.with_nonstandard_names)
        actual = _render_string(_tokenize(node))
        expected = 'def with_nonstandard_names(self, *vars, **kwds)'
        _check(actual, expected, SpecialArgsClient)

    def test_no_args(self):
        node = FunctionNode("test", None, obj=SpecialArgsClient.with_no_args)
        actual = _render_string(_tokenize(node))
        expected = 'def with_no_args(self)'
        _check(actual, expected, SpecialArgsClient)

    def test_keyword_only_args(self):
        node = FunctionNode("test", None, obj=SpecialArgsClient.with_keyword_only_args)
        actual = _render_string(_tokenize(node))
        expected = 'def with_keyword_only_args(self, *, value, **kwargs)'
        _check(actual, expected, SpecialArgsClient)
        
    def test_positional_only_args(self):
        node = FunctionNode("test", None, obj=SpecialArgsClient.with_positional_only_args)
        actual = _render_string(_tokenize(node))
        expected = 'def with_positional_only_args(self, a, b, /, c)'
        _check(actual, expected, SpecialArgsClient)
