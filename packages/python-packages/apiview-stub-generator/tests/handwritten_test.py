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
        """Test that HandwrittenExtendedClass tokens have 'handwritten' in render_classes.

        Tests handwritten class, methods, overloads, properties, ivars, cvars.
        """
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
                # If handwritten line found, move to next line
                if handwritten_line:
                    break
            # If no tokens in line are in handwritten_names, then they should be generated
            if not handwritten_line:
                try:
                    # If generated, check that tokens don't have "handwritten"
                    self._check_tokens_generated(line["Tokens"])
                except AssertionError as exc:
                    # since decorators are on prior ReviewLines from the func def signature line, these will error
                    if "should not have 'handwritten'" in str(exc) and self._is_decorator_line(line["Tokens"]):
                        # skip since handwritten decorator will be checked during method line check
                        pass
                    else:
                        raise
    
    def _check_decorators_handwritten(self, review_lines, prev_line_idx, related_to_line):
        # check for any and all decorators preceding line
        while prev_line_idx > 0 and review_lines[prev_line_idx]["Tokens"][0]["Value"].startswith("@"):
            # double check that the RelatedToLine for the decorator is the same as the LineID
            assert review_lines[prev_line_idx]["LineId"] == related_to_line, \
                f"Expected related line ID {related_to_line}, but got {review_lines[prev_line_idx]['LineId']}"
            # check that the decorator tokens are all handwritten
            self._check_tokens_handwritten(review_lines[prev_line_idx]["Tokens"])
            # keep going until no decorators found on previous line
            prev_line_idx -= 1
    
    def _is_decorator_line(self, tokens):
        if tokens and tokens[0]["Value"].startswith("@"):
            return True
        return False

    def _check_tokens_handwritten(self, tokens):
        for token in tokens:
            assert "RenderClasses" in token and "handwritten" in token["RenderClasses"], \
                f"Token {token['Value']} should have 'handwritten' render class"

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
