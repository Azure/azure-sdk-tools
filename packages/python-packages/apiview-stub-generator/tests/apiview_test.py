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
from importlib.util import find_spec


class TestApiView:
    def _count_newlines(self, apiview: ApiView):
        newline_count = 0
        for line in apiview.review_lines[::-1]:
            if len(line.tokens) == 0:
                newline_count += 1
            else:
                break
        return newline_count

    # Validates that there are no repeat defintion IDs and that each line has only one definition ID.
    def _validate_line_ids(self, apiview: ApiView):
        line_ids = set()

        def collect_line_ids(review_lines, index=0):
            for line in review_lines:
                # Ensure that there are no repeated definition IDs.
                if line.line_id and line.line_id in line_ids:
                    fail(f"Duplicate definition ID {line.line_id}.")
                    line_ids.add(line.line_id)
                # Recursively collect definition IDs from child lines
                if line.children:
                    collect_line_ids(line.children, index)

        collect_line_ids(apiview.review_lines)

    def test_optional_dependencies(self):
        pkg_path = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "apistubgentest"))
        temp_path = tempfile.gettempdir()
        stub_gen = StubGenerator(pkg_path=pkg_path, temp_path=temp_path)
        apiview = stub_gen.generate_tokens()
        assert find_spec("httpx") is not None
        assert find_spec("pandas") is not None
        # skip conditional optional dependencies
        assert find_spec("qsharp") is None

    def test_multiple_newline_only_add_one(self):
        apiview = ApiView()
        review_line = apiview.review_lines.create_review_line()
        review_line.add_text("Something")
        apiview.review_lines.append(review_line)
        apiview.review_lines.set_blank_lines()
        # subsequent calls result in no change
        apiview.review_lines.set_blank_lines()
        apiview.review_lines.set_blank_lines()
        assert self._count_newlines(apiview) == 1

    def test_set_blank_lines(self):
        apiview = ApiView()
        apiview.review_lines.set_blank_lines(3)
        assert self._count_newlines(apiview) == 3

        review_line = apiview.review_lines.create_review_line()
        review_line.add_text("Something")
        apiview.review_lines.set_blank_lines(1)
        apiview.review_lines.set_blank_lines(5)
        # only the last invocation matters
        apiview.review_lines.set_blank_lines(2)
        assert self._count_newlines(apiview) == 2

    def test_api_view_diagnostic_warnings(self):
        pkg_path = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "apistubgentest"))
        temp_path = tempfile.gettempdir()
        stub_gen = StubGenerator(pkg_path=pkg_path, temp_path=temp_path)
        apiview = stub_gen.generate_tokens()
        # ensure we have only the expected diagnostics when testing apistubgentest
        unclaimed = PylintParser.get_unclaimed()
        assert len(apiview.diagnostics) == 22
        # The "needs copyright header" error corresponds to a file, which isn't directly
        # represented in APIView
        assert len(unclaimed) == 1

    def test_add_type(self):
        apiview = ApiView()
        review_line = apiview.review_lines.create_review_line()
        review_line.add_type(type_name="a.b.c.1.2.3.MyType", apiview=apiview)
        apiview.review_lines.append(review_line)
        tokens = review_line.tokens
        assert len(tokens) == 1
        assert tokens[0].kind == TokenKind.TYPE_NAME

    def test_line_ids(self):
        pkg_path = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "apistubgentest"))
        temp_path = tempfile.gettempdir()
        stub_gen = StubGenerator(pkg_path=pkg_path, temp_path=temp_path)
        apiview = stub_gen.generate_tokens()
        self._validate_line_ids(apiview)

    def test_mapping_file(self):
        pkg_path = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "apistubgentest"))
        mapping_path = os.path.abspath(
            os.path.join(os.path.dirname(__file__), "..", "..", "apistubgentest", "apiview_mapping_python.json")
        )
        temp_path = tempfile.gettempdir()
        stub_gen = StubGenerator(pkg_path=pkg_path, temp_path=temp_path, mapping_path=mapping_path)
        apiview = stub_gen.generate_tokens()
        self._validate_line_ids(apiview)
        cross_language_lines = []

        def get_cross_language_id(review_lines):
            for line in review_lines:
                if line.cross_language_id:
                    cross_language_lines.append(line)
                if line.children:
                    get_cross_language_id(line.children)

        get_cross_language_id(apiview.review_lines)
        assert cross_language_lines[0].cross_language_id == "Formal_Model_Id"
        assert cross_language_lines[1].cross_language_id == "Docstring_DocstringWithFormalDefault"
        assert len(cross_language_lines) == 2
        assert apiview.cross_language_package_id == "ApiStubGenTest"

    def test_source_url(self):
        pkg_path = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "apistubgentest"))
        temp_path = tempfile.gettempdir()
        stub_gen = StubGenerator(pkg_path=pkg_path, temp_path=temp_path, source_url="https://www.bing.com/")
        apiview = stub_gen.generate_tokens()
        # Check that TokenKind is EXTERNAL_URL
        assert apiview.review_lines[2]["Tokens"][1]["Kind"] == 8
