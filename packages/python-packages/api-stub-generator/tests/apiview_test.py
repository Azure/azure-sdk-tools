# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from unicodedata import name
from apistub import ApiView, TokenKind, StubGenerator
import os
import tempfile


class StubGenTestArgs:
    pkg_path = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', 'apistubgentest'))
    temp_path = tempfile.gettempdir()
    out_path = None
    mapping_path = None
    hide_report = None
    verbose = None
    filter_namespace = None


class TestApiView:
    def _count_newlines(self, apiview):
        newline_count = 0
        for token in apiview.tokens[::-1]:
            if token.kind == TokenKind.Newline:
                newline_count += 1
            else:
                break
        return newline_count

    def test_multiple_newline_only_add_one(self):
        apiview = ApiView()
        apiview.add_text(None, "Something")
        apiview.add_newline()
        # subsequent calls result in no change
        apiview.add_newline()
        apiview.add_newline()
        assert self._count_newlines(apiview) == 1

    def test_set_blank_lines(self):
        apiview = ApiView()
        apiview.set_blank_lines(3)
        assert self._count_newlines(apiview) == 4 # +1 for carriage return

        apiview.add_text(None, "Something")
        apiview.add_newline()
        apiview.set_blank_lines(1)
        apiview.set_blank_lines(5)
        # only the last invocation matters
        apiview.set_blank_lines(2)
        assert self._count_newlines(apiview) == 3 # +1 for carriage return

    def test_api_view_diagnostic_warnings(self):
        args = StubGenTestArgs()
        print(args.pkg_path)
        stub_gen = StubGenerator(args=args)
        apiview = stub_gen.generate_tokens()
        # ensure we have only the expected diagnostics when testing apistubgentest
        # TODO: These will be removed soon.
        assert len(apiview.diagnostics) == 21
