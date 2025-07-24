# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

import pytest
from apistub.nodes import ClassNode
from apistubgentest import HandwrittenExtendedClass, HandwrittenEnum, HandwrittenDict
from ._test_util import _tokenize, MockApiView


class TestHandwrittenTokens:
    """Test that handwritten classes and functions have proper 'handwritten' render classes."""
    
    pkg_namespace = "apistubgentest"

    def test_handwritten_extended_class_tokens(self):
        """Test that HandwrittenExtendedClass tokens have 'handwritten' in render_classes."""
        obj = HandwrittenExtendedClass
        class_node = ClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        tokens = _tokenize(class_node)
        handwritten = [
            "__init__",
            "handwritten_process", # overload
            "get_summary", # multi-line method
            "validate_data", # single-line method
            "handwritten_property", # property
            "handwritten_name", # ivar
            "handwritten_class_var", # cvar
        ]
        self._check_class_handwritten(tokens, obj.__name__)
        self._check_handwritten_names(tokens[1]["Children"], handwritten) 
        # Check that adding a generated method to handwritten fails
        some_generated = handwritten + ["something"]
        with pytest.raises(AssertionError, match="should have 'handwritten'"):
            self._check_handwritten_names(tokens[1]["Children"], some_generated)
    
    def test_handwritten_enum_tokens(self):
        obj = HandwrittenEnum
        class_node = ClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        tokens = _tokenize(class_node)
        self._check_enum_handwritten(tokens, obj.__name__)

    def test_handwritten_typed_dict_tokens(self):
        obj = HandwrittenDict
        class_node = ClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        tokens = _tokenize(class_node)
        for line in tokens:
            print(line)
        self._check_typed_dict_handwritten(tokens, obj.__name__)

    def _check_enum_handwritten(self, review_lines, enum_name):
        for line_idx, line in enumerate(review_lines):
            for token in line["Tokens"]:
                if token["Value"] == enum_name:
                    self._check_tokens_handwritten(line["Tokens"])
                for child_line in line["Children"]:
                    self._check_tokens_handwritten(child_line["Tokens"])

    def _check_typed_dict_handwritten(self, review_lines, typed_dict_name):
        for line_idx, line in enumerate(review_lines):
            for token in line["Tokens"]:
                if token["Value"] == typed_dict_name:
                    self._check_tokens_handwritten(line["Tokens"])
                    for child_line in line["Children"]:
                        self._check_tokens_handwritten(child_line["Tokens"])

    # Check that class definition tokens have 'handwritten' in RenderClasses
    def _check_class_handwritten(self, review_lines, class_name):
        for line_idx, line in enumerate(review_lines):
            for token in line["Tokens"]:
                if token["Value"] == class_name:
                    # check that any class decorators are handwritten
                    self._check_decorators_handwritten(review_lines, line_idx - 1, token["LineId"])
                    self._check_tokens_handwritten(line["Tokens"])
                    break
            break

    def _check_handwritten_names(self, review_lines, handwritten_names):
        for line_idx, line in enumerate(review_lines):
            # Track whether handwritten or not, to check generated later
            handwritten_line = False
            for token in line["Tokens"]:
                if token["Value"] in handwritten_names:
                    handwritten_line = True
                    # check that all tokens in current line are handwritten
                    self._check_tokens_handwritten(line["Tokens"])
                    # method
                    if "RenderClass" in token and "method" in token["RenderClasses"]:
                        # check that any method decorators are handwritten
                        self._check_decorators_handwritten(review_lines, line_idx - 1, token["LineId"])
                        # multi-line method
                        if "Children" in line:
                            # check handwritten multi-line methods
                            for child_line in line["Children"]:
                                self._check_tokens_handwritten(child_line["Tokens"])
                # If handwritten line, move to next line
                if handwritten_line:
                    break
            # If no tokens in line are in handwritten_names, then they should be generated
            if not handwritten_line:
                try:
                    # If generated, check that tokens don't have "handwritten"
                    self._check_tokens_generated(line["Tokens"])
                except AssertionError as exc:
                    if "should not have 'handwritten'" in str(exc) and self._is_decorator_line(line["Tokens"]):
                        # handwritten decorator will be checked during method line check
                        pass
                    else:
                        raise
    
    def _check_decorators_handwritten(self, review_lines, prev_line_idx, related_to_line):
        while prev_line_idx > 0 and review_lines[prev_line_idx]["Tokens"][0]["Value"].startswith("@"):
            assert review_lines[prev_line_idx]["LineId"] == related_to_line, \
                f"Expected related line ID {related_to_line}, but got {review_lines[prev_line_idx]['LineId']}"
            self._check_tokens_handwritten(review_lines[prev_line_idx]["Tokens"])
            prev_line_idx -= 1
    
    def _is_decorator_line(self, tokens):
        if tokens and tokens[0]["Value"].startswith("@"):
            return True
        return False

    def _check_tokens_handwritten(self, tokens):
        for token in tokens:
            assert "RenderClasses" in token and "handwritten" in token["RenderClasses"], \
                f"Token {token['Value']} should have 'handwritten' render class"

    def _check_decorators_generated(self, review_lines, prev_line_idx, related_to_line):
        while prev_line_idx > 0 and review_lines[prev_line_idx]["Tokens"][0]["Value"].startswith("@"):
            assert review_lines[prev_line_idx]["LineId"] == related_to_line, \
                f"Expected related line ID {related_to_line}, but got {review_lines[prev_line_idx]['LineId']}"
            self._check_tokens_generated(review_lines[prev_line_idx]["Tokens"])
            prev_line_idx -= 1
    
    def _check_tokens_generated(self, tokens):
        for token in tokens:
            # Generated classes should not have "handwritten"
            assert "RenderClasses" not in token or "handwritten" not in token["RenderClasses"], \
                f"Token {token['Value']} should not have 'handwritten' render class"

            # Only multi-line method should have children. Check all child tokens are not "handwritten".
            if "Children" in token:
                for child_line in token["Children"]:
                    for child_token in child_line:
                        assert "RenderClasses" not in child_token or "handwritten" not in child_token["RenderClasses"], \
                            f"Token {child_token['Value']} should not have 'handwritten' render class"

    def test_inherited_functions_not_handwritten(self):
        """Test that inherited functions do NOT have 'handwritten' in render_classes."""
        obj = HandwrittenExtendedClass
        class_node = ClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        tokens = _tokenize(class_node)
        
        # Find inherited methods (they should NOT have 'handwritten')
        # These are methods inherited from base classes, not defined in _patch.py
        inherited_methods = ["do_thing", "double", "something", "mixin_overloaded_method"]
        self._check_inherited_not_handwritten(tokens, inherited_methods)

    def _check_inherited_not_handwritten(self, review_lines, inherited_methods):
        """Check that inherited methods do not have 'handwritten' render class."""
        for line in review_lines:
            if line.line_id:
                for method_name in inherited_methods:
                    if method_name in line.line_id:
                        # Inherited methods should NOT have 'handwritten'
                        for token in line.tokens:
                            if hasattr(token, 'render_classes') and token.render_classes:
                                assert 'handwritten' not in token.render_classes, \
                                    f"Inherited method '{method_name}' should not have 'handwritten' render class"
            
            # Recursively check children
            if hasattr(line, 'children') and line.children:
                self._check_inherited_not_handwritten(line.children, inherited_methods)

    def test_handwritten_functions_have_handwritten_tokens(self):
        """Test that all tokens for handwritten functions have 'handwritten' render class."""
        obj = HandwrittenExtendedClass
        class_node = ClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        tokens = _tokenize(class_node)
        
        # Functions defined in _patch.py should have handwritten tokens
        handwritten_functions = [
            "handwritten_process", "__init__", 
            "get_summary", "validate_data"
        ]
        self._check_function_tokens_handwritten(tokens, handwritten_functions)

    def _check_function_tokens_handwritten(self, review_lines, function_names):
        """Check that all tokens in handwritten functions have 'handwritten' render class."""
        for line in review_lines:
            if line.line_id:
                for func_name in function_names:
                    if func_name in line.line_id:
                        # All tokens in handwritten functions should have 'handwritten'
                        self._check_all_tokens_in_line_handwritten(line, func_name)
            
            # Recursively check children
            if hasattr(line, 'children') and line.children:
                self._check_function_tokens_handwritten(line.children, function_names)

    def _check_all_tokens_in_line_handwritten(self, line, context_name):
        """Check that all tokens in a line and its children have 'handwritten' render class."""
        for token in line.tokens:
            if hasattr(token, 'render_classes') and token.render_classes:
                assert 'handwritten' in token.render_classes, \
                    f"Token '{token.value}' in {context_name} missing 'handwritten' render class"
        
        # Check children tokens too (for multi-line functions)
        if hasattr(line, 'children') and line.children:
            for child_line in line.children:
                self._check_all_tokens_in_line_handwritten(child_line, context_name)
