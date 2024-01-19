# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from apistub import ApiView, TokenKind, StubGenerator
from apistub.nodes import PylintParser
import os
from pytest import fail
import tempfile


class TestApiView:
    def _count_newlines(self, apiview: ApiView):
        newline_count = 0
        for token in apiview.tokens[::-1]:
            if token.kind == TokenKind.Newline:
                newline_count += 1
            else:
                break
        return newline_count

    # Validates that there are no repeat defintion IDs and that each line has only one definition ID.
    def _validate_definition_ids(self, apiview: ApiView):
        definition_ids = set()
        def_ids_per_line = [[]]
        index = 0
        for token in apiview.tokens:
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
        

    def test_multiple_newline_only_add_one(self):
        apiview = ApiView()
        apiview.add_text("Something")
        apiview.add_newline()
        # subsequent calls result in no change
        apiview.add_newline()
        apiview.add_newline()
        assert self._count_newlines(apiview) == 1

    def test_set_blank_lines(self):
        apiview = ApiView()
        apiview.set_blank_lines(3)
        assert self._count_newlines(apiview) == 4 # +1 for carriage return

        apiview.add_text("Something")
        apiview.add_newline()
        apiview.set_blank_lines(1)
        apiview.set_blank_lines(5)
        # only the last invocation matters
        apiview.set_blank_lines(2)
        assert self._count_newlines(apiview) == 3 # +1 for carriage return

    def test_api_view_diagnostic_warnings(self):
        pkg_path = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "apistubgentest"))
        temp_path = tempfile.gettempdir()
        stub_gen = StubGenerator(pkg_path=pkg_path, temp_path=temp_path)
        apiview = stub_gen.generate_tokens()
        # ensure we have only the expected diagnostics when testing apistubgentest
        unclaimed = PylintParser.get_unclaimed()
        assert len(apiview.diagnostics) == 14
        # The "needs copyright header" error corresponds to a file, which isn't directly
        # represented in APIView
        assert len(unclaimed) == 1

    def test_add_type(self):
        apiview = ApiView()
        apiview.tokens = []
        apiview.add_type(type_name="a.b.c.1.2.3.MyType")
        tokens = apiview.tokens
        assert len(tokens) == 2
        assert tokens[0].kind == TokenKind.TypeName
        assert tokens[1].kind == TokenKind.Punctuation

    def test_definition_ids(self):
        pkg_path = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "apistubgentest"))
        temp_path = tempfile.gettempdir()
        stub_gen = StubGenerator(pkg_path=pkg_path, temp_path=temp_path)
        apiview = stub_gen.generate_tokens()
        self._validate_definition_ids(apiview)
