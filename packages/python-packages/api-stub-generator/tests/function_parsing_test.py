# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from apistub.nodes import ClassNode, FunctionNode

from typing import Optional

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


class TestFunctionParsing:
    
    def test_optional_typehint(self):
        func_node = FunctionNode("test", None, TestClass.with_optional_typehint, "test")
        arg = func_node.args["description"]
        assert arg.argtype == "typing.Optional[str]"
        assert arg.default == "None"

    def test_optional_docstring(self):
        func_node = FunctionNode("test", None, TestClass.with_optional_docstring, "test")
        arg = func_node.args["description"]
        assert arg.argtype == "str"
        assert arg.default == "..."
