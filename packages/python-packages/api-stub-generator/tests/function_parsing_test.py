# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from apistub.nodes import ClassNode, FunctionNode

from typing import Optional, Any

class TestClass:
    """ Function parsing tests."""

    def with_optional_typehint(self, *, description: Optional[str] = None):
        self.description = description

    def with_optional_docstring(self, *, description = None):
        """ With docstring
        :keyword description: A description
        :paramtype description: str
        """
        self.description = description

    def with_variadic_python3_typehint(self, *vars: str, **kwargs: "Any") -> None:
        return None

    def with_variadic_python2_typehint(self, *vars, **kwargs):
        # type: (*str, **Any) -> None
        """ With docstring
        :param vars: Variadic argument
        :type vars: str
        """
        return None

    def with_default_values(self, foo="1", *, bar="2", baz=None):
        return None


class TestFunctionParsing:
    
    def test_optional_typehint(self):
        func_node = FunctionNode("test", None, TestClass.with_optional_typehint, "test")
        arg = func_node.args["description"]
        assert arg.argtype == "typing.Optional[str]"
        assert arg.default == "..."

    def test_optional_docstring(self):
        func_node = FunctionNode("test", None, TestClass.with_optional_docstring, "test")
        arg = func_node.args["description"]
        assert arg.argtype == "str"
        assert arg.default == "..."

    def test_variadic_typehints(self):
        func_node = FunctionNode("test", None, TestClass.with_variadic_python3_typehint, "test")
        arg = func_node.args["vars"]
        assert arg.argname == "*vars"
        assert arg.argtype == "str"
        assert arg.default == ""

        func_node = FunctionNode("test", None, TestClass.with_variadic_python2_typehint, "test")
        arg = func_node.args["vars"]
        assert arg.argname == "*vars"
        # the type annotation comes ONLY from the docstring. The Python2 type hint is not used!
        assert arg.argtype == "str"
        assert arg.default == ""

    def test_default_values(self):
        func_node = FunctionNode("test", None, TestClass.with_default_values, "test")
        assert func_node.args["foo"].default == "1"
        assert func_node.kw_args["bar"].default == "2"
        assert func_node.kw_args["baz"].default == "..."
