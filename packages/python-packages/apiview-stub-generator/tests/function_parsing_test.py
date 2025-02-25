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
    SpecialArgsClient,
)

from ._test_util import _render_lines, _check, _tokenize, MockApiView, _count_review_line_metadata


def _test_arg(node, key, name, _type, default):
    arg = node.args[key]
    assert arg.argname == name
    assert arg.argtype == _type
    assert arg.default == default


def _test_return(node, _type):
    assert node.return_type == _type


class TestTypeHints:
    """Ensure simple typehints render correctly."""

    def test_simple_typehints(self):
        clients = [Python2TypeHintClient, Python3TypeHintClient, DocstringTypeHintClient]
        for client in clients:
            node = FunctionNode("test", None, apiview=MockApiView, obj=client.with_simple_typehints)
            actual = _render_lines(_tokenize(node))
            expected = [
                "def with_simple_typehints(",
                "    self, ",
                "    name: str, ",
                "    age: int",
                ") -> str",
                ""
            ]
            _check(actual, expected, client)

    """ Ensure complicated typehints render correctly. """

    def test_complex_typehints(self):
        clients = [Python2TypeHintClient, Python3TypeHintClient, DocstringTypeHintClient]
        for client in clients:
            node = FunctionNode("test", None, apiview=MockApiView, obj=client.with_complex_typehints)
            actual = _render_lines(_tokenize(node))
            expected = "def with_complex_typehints(self, value: List[ItemPaged[Union[FakeObject, FakeError]]]) -> None"
            expected = [expected, ""]
            _check(actual, expected, client)

    """ Ensure typehints for *args and **kwargs render correctly. Note that Py2-style typehints are not supported. """

    def test_variadic_typehints(self):
        clients = [
            # TODO: Known limitation of astroid (See: https://github.com/Azure/azure-sdk-tools/issues/3131)
            # Python2TypeHintClient,
            Python3TypeHintClient,
            DocstringTypeHintClient,
        ]
        for client in clients:
            node = FunctionNode("test", None, apiview=MockApiView, obj=client.with_variadic_typehint)
            tokens = _tokenize(node)
            actual = _render_lines(tokens)
            expected = [
                "def with_variadic_typehint(",
                "    self, ",
                "    *vars: str, ",
                "    **kwargs: Any",
                ") -> None",
                ""
            ]
            _check(actual, expected, client)

            metadata = {"RelatedToLine": 0, "IsContextEndLine": 0}
            metadata = _count_review_line_metadata(tokens, metadata)
            assert metadata["RelatedToLine"] == 0
            assert metadata["IsContextEndLine"] == 1

    """ Ensure list type return typehint renders correctly. """

    def test_str_list_return_type(self):
        clients = [Python2TypeHintClient, Python3TypeHintClient, DocstringTypeHintClient]
        for client in clients:
            node = FunctionNode("test", None, apiview=MockApiView, obj=client.with_str_list_return_type)
            actual = _render_lines(_tokenize(node))
            expected = "def with_str_list_return_type(self) -> List[str]"
            expected = [expected, ""]
            _check(actual, expected, client)

    """ Ensure list return type typehint renders correctly with a class. """

    def test_list_return_type(self):
        clients = [Python2TypeHintClient, Python3TypeHintClient, DocstringTypeHintClient]
        for client in clients:
            node = FunctionNode("test", None, apiview=MockApiView, obj=client.with_list_return_type)
            actual = _render_lines(_tokenize(node))
            expected = "def with_list_return_type(self) -> List[TestClass]"
            expected = [expected, ""]
            _check(actual, expected, client)

    """ Ensure union return type typehint renders correctly. """

    def test_list_union_return_type(self):
        clients = [Python2TypeHintClient, Python3TypeHintClient, DocstringTypeHintClient]
        for client in clients:
            node = FunctionNode("test", None, apiview=MockApiView, obj=client.with_list_union_return_type)
            actual = _render_lines(_tokenize(node))
            expected = "def with_list_union_return_type(self) -> List[Union[str, int]]"
            expected = [expected, ""]
            _check(actual, expected, client)

    """ Ensure datetime typehint renders correctly. """

    def test_datetime_typehint(self):
        clients = [Python2TypeHintClient, Python3TypeHintClient, DocstringTypeHintClient]
        for client in clients:
            node = FunctionNode("test", None, apiview=MockApiView, obj=client.with_datetime_typehint)
            actual = _render_lines(_tokenize(node))
            expected = "def with_datetime_typehint(self, date: datetime) -> datetime"
            expected = [expected, ""]
            _check(actual, expected, client)


class TestDefaultValues:
    """Ensure that simple default values appear correctly."""

    def test_simple_default(self):
        node = FunctionNode("test", None, apiview=MockApiView, obj=DefaultValuesClient.with_simple_default)
        tokens = _tokenize(node)
        actual = _render_lines(tokens)
        expected = [
            "def with_simple_default(",
            '    name: str = "Bill", ',
            "    *, ",
            "    age: int = 21",
            ") -> None",
            ""
        ]
        _check(actual, expected, DefaultValuesClient)
        metadata = {"RelatedToLine": 0, "IsContextEndLine": 0}
        metadata = _count_review_line_metadata(tokens, metadata)
        assert metadata["RelatedToLine"] == 0
        assert metadata["IsContextEndLine"] == 1

    """ Ensure that optional types with defaults display correctly. """

    def test_simple_optional_default(self):
        node = FunctionNode("test", None, apiview=MockApiView, obj=DefaultValuesClient.with_simple_optional_defaults)
        actual = _render_lines(_tokenize(node))
        expected = [
            "def with_simple_optional_defaults(",
            '    name: Optional[str] = "Bill", ',
            "    *, ",
            "    age: Optional[int] = 21",
            ") -> None",
            ""
        ]
        _check(actual, expected, DefaultValuesClient)

    """ Ensure that falsy defaults for Optional kwargs do not translate to ... """

    def test_falsy_optional_defaults(self):
        node = FunctionNode("test", None, apiview=MockApiView, obj=DefaultValuesClient.with_falsy_optional_defaults)
        actual = _render_lines(_tokenize(node))
        expected = [
            "def with_falsy_optional_defaults(",
            "    *, ",
            "    bool: Optional[bool] = False, ",
            "    int: Optional[int] = 0, ",
            '    string: Optional[str] = ""',
            ") -> None",
            ""
        ]
        _check(actual, expected, DefaultValuesClient)

    """ Ensure that falsy defaults for Optional kwargs do not translate to ... when a docstring is provided. """

    def test_falsy_optional_defaults_and_docstring(self):
        node = FunctionNode(
            "test", None, apiview=MockApiView, obj=DefaultValuesClient.with_falsy_optional_defaults_and_docstring
        )
        actual = _render_lines(_tokenize(node))
        expected = [
            "def with_falsy_optional_defaults_and_docstring(",
            "    *, ",
            "    bool: Optional[bool] = False, ",
            "    int: Optional[int] = 0, ",
            '    string: Optional[str] = ""',
            ") -> None",
            ""
        ]
        _check(actual, expected, DefaultValuesClient)

    """ Ensures that optional values appear with the correct default annotation. `None` for normal args and `...` for kwargs. """

    def test_optional_none_default(self):
        node = FunctionNode("test", None, apiview=MockApiView, obj=DefaultValuesClient.with_optional_none_defaults)
        actual = _render_lines(_tokenize(node))
        expected = [
            "def with_optional_none_defaults(",
            "    name: Optional[str] = None, ",
            "    *, ",
            "    age: Optional[int] = ...",
            ") -> None",
            ""
        ]
        _check(actual, expected, DefaultValuesClient)

    """ Ensure that a default value that is a class type appears correctly. """

    def test_class_default(self):
        node = FunctionNode("test", None, apiview=MockApiView, obj=DefaultValuesClient.with_class_default)
        actual = _render_lines(_tokenize(node))
        expected = "def with_class_default(my_class: Any = FakeObject) -> None"
        expected = [expected, ""]
        _check(actual, expected, DefaultValuesClient)

    """ Ensure docstring-parsed defaults are displayed. Note that if they cannot be cast they will appear as strings. """

    def test_parsed_docstring_defaults(self):
        node = FunctionNode("test", None, apiview=MockApiView, obj=DefaultValuesClient.with_parsed_docstring_defaults)
        actual = _render_lines(_tokenize(node))
        expected = [
            "def with_parsed_docstring_defaults(",
            '    name: str = "Bill", ',
            "    age: int = 21, ",
            '    some_class: class = ":py:class:`apistubgen.test.models.FakeObject`"',
            ") -> None",
            ""
        ]
        _check(actual, expected, DefaultValuesClient)

    """ Ensure string-based enum default values can specify either a string or an enum value. """

    def test_enum_defaults(self):
        node = FunctionNode("test", None, apiview=MockApiView, obj=DefaultValuesClient.with_enum_defaults)
        actual = _render_lines(_tokenize(node))
        expected = 'def with_enum_defaults(enum1: Union[PetEnumPy3Metaclass, str] = "DOG", enum2: Union[PetEnumPy3Metaclass, str] = PetEnumPy3Metaclass.DOG) -> None'
        expected = [expected, ""]
        _check(actual, expected, DefaultValuesClient)


class TestSpecialArguments:
    """Ensure the variadic and keyword-only arguments have the correct prefixes."""

    def test_standard_names(self):
        node = FunctionNode("test", None, apiview=MockApiView, obj=SpecialArgsClient.with_standard_names)
        actual = _render_lines(_tokenize(node))
        expected = [
            "def with_standard_names(",
            "    self, ",
            "    *args, ",
            "    **kwargs",
            ") -> None",
            ""
        ]
        _check(actual, expected, SpecialArgsClient)

    """ Ensure the variadic and keyword-only arguments can be given custom names. """

    def test_nonstandard_names(self):
        node = FunctionNode("test", None, apiview=MockApiView, obj=SpecialArgsClient.with_nonstandard_names)
        actual = _render_lines(_tokenize(node))
        expected = [
            "def with_nonstandard_names(",
            "    self, ",
            "    *vars, ",
            "    **kwds",
            ") -> None",
            ""
        ]
        _check(actual, expected, SpecialArgsClient)

    """ Ensure a basic function with no args renders appropriately. """

    def test_no_args(self):
        node = FunctionNode("test", None, apiview=MockApiView, obj=SpecialArgsClient.with_no_args)
        actual = _render_lines(_tokenize(node))
        expected = "def with_no_args() -> None"
        expected = [expected, ""]
        _check(actual, expected, SpecialArgsClient)

    """ Ensure keyword only argument marker (*) is displayed. """

    def test_keyword_only_args(self):
        node = FunctionNode("test", None, apiview=MockApiView, obj=SpecialArgsClient.with_keyword_only_args)
        actual = _render_lines(_tokenize(node))
        expected = "def with_keyword_only_args(self, *, value, **kwargs) -> None"
        expected = [
            "def with_keyword_only_args(",
            "    self, ",
            "    *, ",
            "    value, ",
            "    **kwargs",
            ") -> None",
            ""
        ]
        _check(actual, expected, SpecialArgsClient)

    """ Ensure positional only argument marker (/) is displayed. """

    def test_positional_only_args(self):
        node = FunctionNode("test", None, apiview=MockApiView, obj=SpecialArgsClient.with_positional_only_args)
        actual = _render_lines(_tokenize(node))
        expected = [
            "def with_positional_only_args(",
            "    self, ",
            "    a, ",
            "    b, ",
            "    /, ",
            "    c",
            ") -> None",
            ""
        ]
        _check(actual, expected, SpecialArgsClient)

    """ Ensure kwargs are sorted alphabetically. """

    def test_alphabetical_kwargs(self):
        node = FunctionNode("test", None, apiview=MockApiView, obj=SpecialArgsClient.with_sorted_kwargs)
        actual = _render_lines(_tokenize(node))
        expected = [
            "def with_sorted_kwargs(",
            "    self, ",
            "    *, ",
            "    a, ",
            "    b, ",
            "    c, ",
            "    d, ",
            "    **kwargs",
            ") -> None",
            ""
        ]
        _check(actual, expected, SpecialArgsClient)
