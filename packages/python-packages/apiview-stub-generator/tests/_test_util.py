# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from apistub import ApiView
from typing import List


def _tokenize(node):
    apiview = ApiView(pkg_name="test", namespace="test")
    # pass through apiview for diagnostics
    apiview.review_lines.apiview = apiview
    node.generate_tokens(apiview.review_lines)
    return apiview.review_lines


""" Returns the review line tokens rendered into distinct lines. """
def _render_lines(review_lines) -> List[str]:
    return review_lines.render()


""" Returns the review line tokens as a single concatenated string. """
def _render_string(tokens) -> str:
    lines = "".join([x.render() for x in tokens]).splitlines()
    return _merge_lines(lines)


""" Merges the provided lines together, removing any leading whitespace."""
def _merge_lines(lines) -> str:
    return "".join([x.lstrip() for x in lines])


def _check(actual, expected, client):
    assert actual.lstrip() == expected, f"\n*******\nClient: {client.__name__}\nActual:   {actual}\nExpected: {expected}\n*******"    
