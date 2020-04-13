# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from apistub.nodes import DocstringParser

docstring_standard_return_type = """
Dummy docstring to verify standard return types and param types
:rtype: str
"""

docstring_Union_return_type1 = """
Dummy docstring to verify standard return types and param types
:rtype: Union[str, int]
"""

docstring_Union_return_type2 = """
Dummy docstring to verify standard return types and param types
:rtype: Union(str, int)
Dummy string at new line
"""

docstring_union_return_type3 = """
Dummy docstring to verify standard return types and param types
:rtype: union[str, int]
"""

docstring_multi_ret_type = """
Dummy docstring to verify standard return types and param types
:rtype: str or ~azure.test.testclass or None
"""

class TestDocStringParser:

    def _test_return_type(self, docstring, expected):
        docstring_parser = DocstringParser(docstring)
        print(docstring_parser.find_return_type())
        assert expected == docstring_parser.find_return_type()


    def _test_variable_type(self, docstring, varname, expected):
        docstring_parser = DocstringParser(docstring)
        assert expected == docstring_parser.find_type("type", varname)

    def _test_find_args(self, docstring, expected_args, is_keyword = False):
        parser = DocstringParser(docstring)
        expected = {}
        for arg in expected_args:
            expected_args[arg.name] = arg

        for arg in parser.find_args('keyword' if is_keyword else 'param'):
            assert arg.name in expected and arg.type == expected[arg.name].type
            
            
    def test_return_builtin_return_type(self):
        self._test_return_type(docstring_standard_return_type, "str")

    def test_return_union_return_type(self):
        self._test_return_type(docstring_Union_return_type1, "Union[str, int]")
    
    def test_return_union_return_type1(self):
        self._test_return_type(docstring_Union_return_type2, "Union(str, int)")

    def test_return_union_lower_case_return_type(self):
        self._test_return_type(docstring_union_return_type3, "union[str, int]")

    def test_multi_return_type(self):
        self._test_return_type(docstring_multi_ret_type, "str or ~azure.test.testclass or None")