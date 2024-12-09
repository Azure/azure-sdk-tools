# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from apistub import ApiView
from typing import List


def _tokenize(node):
    apiview = ApiView(pkg_name="test", namespace="test")
    apiview.tokens = []
    node.generate_tokens(apiview)
    return apiview.tokens


""" Returns the tokens rendered into distinct lines. """
def _render_lines(tokens) -> List[str]:
    return "".join([x.render() for x in tokens]).splitlines()


""" Returns the tokens as a single concatenated string. """
def _render_string(tokens) -> str:
    lines = "".join([x.render() for x in tokens]).splitlines()
    return _merge_lines(lines)


""" Merges the provided lines together, removing any leading whitespace."""
def _merge_lines(lines) -> str:
    return "".join([x.lstrip() for x in lines])


def _check(actual, expected, client):
    assert actual.lstrip() == expected, f"\n*******\nClient: {client.__name__}\nActual:   {actual}\nExpected: {expected}\n*******"    

def _check_all(actual, expect, obj):
    for (idx, exp) in enumerate(expect):
        act = actual[idx]
        _check(act.lstrip(), exp, obj)
