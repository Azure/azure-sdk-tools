# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from apistub import ApiView, TokenKind, StubGenerator
from apistub.nodes import PylintParser
import os
import tempfile

from ._test_util import _check, _render_string, _tokenize

class StubGenTestArgs:
    pkg_path = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', 'apistubgentest'))
    temp_path = tempfile.gettempdir()
    source_url = None
    out_path = None
    mapping_path = None
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
        args = StubGenTestArgs()
        stub_gen = StubGenerator(args=args)
        apiview = stub_gen.generate_tokens()
        # ensure we have only the expected diagnostics when testing apistubgentest
        unclaimed = PylintParser.get_unclaimed()
        assert len(apiview.diagnostics) == 4
        # The "needs copyright header" error corresponds to a file, which isn't directly
        # represented in APIView
        assert len(unclaimed) == 1
