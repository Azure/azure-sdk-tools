# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from apistub import ApiView
from typing import List


def _tokenize(node):
    apiview = ApiView(pkg_name="test", namespace="test")
    node.generate_tokens(apiview.review_lines)
    return apiview.review_lines


""" Returns the review line tokens rendered into distinct lines. """


def _render_lines(review_lines) -> List[str]:
    lines = review_lines.render()
    return [x for x in lines]


""" Returns the review line tokens as a single concatenated string. """


def _render_string(review_lines) -> str:
    lines = _render_lines(review_lines)
    return _merge_lines(lines)


""" Merges the provided lines together, removing any leading whitespace."""


def _merge_lines(lines) -> str:
    return "".join([x for x in lines])


def _check(actual, expected, client):
    assert len(actual) == len(expected), f"\n*******\nClient: {client.__name__}\nActual:   {actual}\nExpected: {expected}\n*******"
    for i in range(len(expected)):
        assert (
            actual[i] == expected[i]
        ), f"\n*******\nClient: {client.__name__}\nActual:   {actual[i]}\nExpected: {expected[i]}\n*******"

MockApiView = ApiView(pkg_name="test", namespace="test")

def _count_review_line_metadata(tokens, metadata):
    lastRelatedTo = None
    for token in tokens:
        # check that the relatedToLine is the same as the lineID of the next token
        if lastRelatedTo:
            assert token["LineId"] == lastRelatedTo
            lastRelatedTo = None
        # count the number of relatedToLines
        if "RelatedToLine" in token:
            metadata["RelatedToLine"] += 1
            lastRelatedTo = token["RelatedToLine"]
        if "IsContextEndLine" in token and token["IsContextEndLine"]:
            metadata["IsContextEndLine"] += 1
        if "Children" in token:
            _count_review_line_metadata(token["Children"], metadata)

    return metadata
