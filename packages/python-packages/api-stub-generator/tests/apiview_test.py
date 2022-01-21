# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from apistub import ApiView, TokenKind

class TestApiView:

    def _count_newlines(self, apiview):
        newline_count = 0
        for token in apiview.Tokens[::-1]:
            if token.Kind == TokenKind.Newline:
                newline_count += 1
            else:
                break
        return newline_count

    def test_multiple_newline_only_add_one(self):
        apiview = ApiView(None, "test", "0.0", "test")
        apiview.add_text(None, "Something")
        apiview.add_newline()
        # subsequent calls result in no change
        apiview.add_newline()
        apiview.add_newline()
        assert self._count_newlines(apiview) == 1

    def test_set_blank_lines(self):
        apiview = ApiView(None, "test", "0.0", "test")
        apiview.set_blank_lines(3)
        assert self._count_newlines(apiview) == 4 # +1 for carriage return

        apiview.add_text(None, "Something")
        apiview.add_newline()
        apiview.set_blank_lines(1)
        apiview.set_blank_lines(5)
        # only the last invocation matters
        apiview.set_blank_lines(2)
        assert self._count_newlines(apiview) == 3 # +1 for carriage return

