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
    DefaultValuesClient
)


def _test_arg(node, key, name, _type, default):
    arg = node.args[key]
    assert arg.argname == name
    assert arg.argtype == _type
    assert arg.default == default


def _test_return(node, _type):
    assert node.return_type == _type


class TestPython2TypeHints:

    def test_simple_typehints(self):
        node = FunctionNode("test", None, obj=Python2TypeHintClient.with_simple_typehints)
        _test_arg(node, "name", "name", "str", None)
        _test_arg(node, "age", "age", "int", None)

    def test_complex_typehints(self):
        node = FunctionNode("test", None, obj=Python2TypeHintClient.with_complex_typehints)
        _test_arg(node, "value", "value", "List[ItemPaged[Union[FakeObject, FakeError]]]", None)
        
    def test_variadic_typehints(self):
        node = FunctionNode("test", None, obj=Python2TypeHintClient.with_variadic_typehint)
        _test_arg(node, "vars", "*vars", "str", None)
        _test_arg(node, "**kwargs", "**kwargs", "Any", None)

    def test_str_list_return_type(self):
        node = FunctionNode("test", None, obj=Python2TypeHintClient.with_str_list_return_type)
        _test_return(node, "List[str]")

    def test_list_return_type(self):
        node = FunctionNode("test", None, obj=Python2TypeHintClient.with_list_return_type)
        _test_return(node, "List[TestClass]")

    def test_list_union_return_type(self):
        node = FunctionNode("test", None, obj=Python2TypeHintClient.with_list_union_return_type)
        _test_return(node, "List[Union[str, int]]")


class TestPython3TypeHints:

    def test_simple_typehints(self):
        node = FunctionNode("test", None, obj=Python3TypeHintClient.with_simple_typehints)
        _test_arg(node, "name", "name", "str", None)
        _test_arg(node, "age", "age", "int", None)

    def test_complex_typehints(self):
        node = FunctionNode("test", None, obj=Python3TypeHintClient.with_complex_typehints)
        _test_arg(node, "value", "value", "List[ItemPaged[Union[FakeObject, FakeError]]]", None)
        
    def test_variadic_typehints(self):
        node = FunctionNode("test", None, obj=Python3TypeHintClient.with_variadic_typehint)
        _test_arg(node, "vars", "*vars", "str", None)
        _test_arg(node, "**kwargs", "**kwargs", "Any", None)

    def test_str_list_return_type(self):
        node = FunctionNode("test", None, obj=Python3TypeHintClient.with_str_list_return_type)
        _test_return(node, "List[str]")

    def test_list_return_type(self):
        node = FunctionNode("test", None, obj=Python3TypeHintClient.with_list_return_type)
        _test_return(node, "List[TestClass]")

    def test_list_union_return_type(self):
        node = FunctionNode("test", None, obj=Python3TypeHintClient.with_list_union_return_type)
        _test_return(node, "List[Union[str, int]]")


class TestDocstringTypeHints:

    def test_simple_typehints(self):
        node = FunctionNode("test", None, obj=DocstringTypeHintClient.with_simple_typehints)
        _test_arg(node, "name", "name", "str", None)
        _test_arg(node, "age", "age", "int", None)

    def test_complex_typehints(self):
        node = FunctionNode("test", None, obj=DocstringTypeHintClient.with_complex_typehints)
        _test_arg(node, "value", "value", "List[ItemPaged[Union[FakeObject, FakeError]]]", None)
        
    def test_variadic_typehints(self):
        node = FunctionNode("test", None, obj=DocstringTypeHintClient.with_variadic_typehint)
        _test_arg(node, "vars", "*vars", "str", None)
        _test_arg(node, "**kwargs", "**kwargs", "Any", None)

    def test_str_list_return_type(self):
        node = FunctionNode("test", None, obj=DocstringTypeHintClient.with_str_list_return_type)
        _test_return(node, "List[str]")

    def test_list_return_type(self):
        node = FunctionNode("test", None, obj=DocstringTypeHintClient.with_list_return_type)
        _test_return(node, "List[TestClass]")

    def test_list_union_return_type(self):
        node = FunctionNode("test", None, obj=DocstringTypeHintClient.with_list_union_return_type)
        _test_return(node, "List[Union[str, int]]")


class TestDefaultValues:

    def test_simple_default(self):
        node = FunctionNode("test", None, obj=DefaultValuesClient.with_simple_default)
        _test_arg(node, "name", "name", "str", "'Bill'")
        _test_arg(node, "age", "age", "int", "21")

    def test_simple_optional_default(self):
        node = FunctionNode("test", None, obj=DefaultValuesClient.with_simple_optional_defaults)
        _test_arg(node, "name", "name", "Optional[str]", "'Bill'")
        _test_arg(node, "age", "age", "Optional[int]", "21")

    def test_optional_none_default(self):
        node = FunctionNode("test", None, obj=DefaultValuesClient.with_optional_none_defaults)
        _test_arg(node, "name", "name", "Optional[str]", "...")
        _test_arg(node, "age", "age", "Optional[int]", "...")

    def test_class_defalt(self):
        node = FunctionNode("test", None, obj=DefaultValuesClient.with_class_default)
        _test_arg(node, "my_class", "my_class", "Any", "FakeObject")

    def test_parsed_docstring_defaults(self):
        node = FunctionNode("test", None, obj=DefaultValuesClient.with_parsed_docstring_defaults)
        _test_arg(node, "name", "name", "str", "'Bill'")
        _test_arg(node, "age", "age", "int", "21")
        _test_arg(node, "some_class", "some_class", "class", ":py:class:`apistubgen.test.models.FakeObject`")
