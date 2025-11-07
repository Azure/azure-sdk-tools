# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring

"""
Tests for ReviewResult class in APIView Copilot.
"""

from src._models import ReviewResult


class DummyLine:
    def __init__(self, line, line_no):
        self.line = line
        self.line_no = line_no


class DummySection:
    def __init__(self, lines):
        self.lines = [DummyLine(line, i + 1) for i, line in enumerate(lines)]

    def idx_for_line_no(self, line_no):
        """Find the index of the line with the given line number."""
        for idx, l in enumerate(self.lines):
            if l.line_no == line_no:
                return idx
        return None


def test_process_comments_with_validation():
    allowed_ids = ["guideline-1"]
    section = DummySection(["bad_code1", "good_code", "bad_code2"])
    comments = [
        {
            "guideline_ids": ["guideline-1"],
            "line_no": 1,
            "bad_code": "bad_code1",
            "suggestion": "fix1",
            "comment": "msg1",
        },
        {
            "guideline_ids": ["not-allowed"],
            "line_no": 3,
            "bad_code": "bad_code2",
            "suggestion": "fix2",
            "comment": "msg2",
        },
        {
            "line_no": 2,
            "bad_code": "good_code",
            "suggestion": "fix3",
            "comment": "msg3",
        },
    ]
    rr = ReviewResult(comments=comments, allowed_ids=allowed_ids, section=section)
    # All comments should be present, but only valid guideline_ids retained
    expected_comments = [
        {"bad_code": "bad_code1", "guideline_ids": ["guideline-1"], "memory_ids": []},
        {"bad_code": "bad_code2", "guideline_ids": [], "memory_ids": []},
        {"bad_code": "good_code", "guideline_ids": [], "memory_ids": []},
    ]
    assert len(rr.comments) == len(expected_comments)
    for comment, expected in zip(rr.comments, expected_comments):
        assert comment.bad_code == expected["bad_code"]
        assert comment.guideline_ids == expected["guideline_ids"]


def test_find_line_number_correction():
    allowed_ids = ["guideline-1"]
    # The section has lines that are slightly different from the comment's bad_code
    section = DummySection(
        [
            "def foo():",  # line 1
            "    x = 1",  # line 2
            "    y = 2",  # line 3
            "    return x + y",  # line 4
            "# end",  # line 5
        ]
    )
    # The comment's bad_code matches line 3, but line_no is off by 1 (should be 3, but is 2)
    comments = [
        {
            "guideline_ids": ["guideline-1"],
            "line_no": 2,  # This is off by one; should be 3
            "bad_code": "    y = 2",
            "suggestion": "    y = 3",
            "comment": "Should use 3 instead of 2",
        },
        {
            "guideline_ids": ["guideline-1"],
            "line_no": 4,  # This is correct
            "bad_code": "    return x + y",
            "suggestion": "    return x - y",
            "comment": "Should subtract instead of add",
        },
        {
            "guideline_ids": ["guideline-1"],
            "line_no": 10,  # This is out of range
            "bad_code": "# end",
            "suggestion": None,
            "comment": "End marker",
        },
    ]
    rr = ReviewResult(comments=comments, allowed_ids=allowed_ids, section=section)
    expected_comments = [
        {"bad_code": "    y = 2", "line_no": 3},
        {"bad_code": "    return x + y", "line_no": 4},
        {"bad_code": "# end", "line_no": 10},
    ]
    assert len(rr.comments) == len(expected_comments)
    for comment, expected in zip(rr.comments, expected_comments):
        assert comment.bad_code == expected["bad_code"]
        assert comment.line_no == expected["line_no"]


def test_blank_review_result_and_extend():
    allowed_ids1 = ["guideline-1"]
    allowed_ids2 = ["guideline-2"]
    section1 = DummySection(
        [
            "foo = 1",  # line 1
            "bar = 2",  # line 2
        ]
    )
    section2 = DummySection(
        [
            "baz = 3",  # line 1
            "qux = 4",  # line 2
        ]
    )
    comments1 = [
        {
            "guideline_ids": ["guideline-1"],
            "line_no": 2,
            "bad_code": "foo = 1",
            "suggestion": "foo = 42",
            "comment": "Change foo",
        },
        {
            "guideline_ids": ["abc-123"],
            "line_no": 2,
            "bad_code": "bar = 2",
            "suggestion": "bar = 22",
            "comment": "Change bar",
        },
    ]
    comments2 = [
        {
            "guideline_ids": ["bad-rule"],
            "line_no": 1,
            "bad_code": "baz = 3",
            "suggestion": "baz = 33",
            "comment": "Change baz",
        },
        {
            "guideline_ids": ["guideline-2", "aaa-bbb-ccc"],
            "line_no": 2,
            "bad_code": "qux = 4",
            "suggestion": "qux = 99",
            "comment": "Change qux",
        },
    ]
    rr_blank = ReviewResult()
    rr1 = ReviewResult(comments=comments1, allowed_ids=allowed_ids1, section=section1)
    rr2 = ReviewResult(comments=comments2, allowed_ids=allowed_ids2, section=section2)
    rr_blank.comments.extend(rr1.comments)  # pylint: disable=no-member
    rr_blank.comments.extend(rr2.comments)  # pylint: disable=no-member

    # Validate all comments are present and correct using a cleaner approach
    expected_comments = [
        {"line_no": 1, "bad_code": "foo = 1", "guideline_ids": ["guideline-1"], "memory_ids": []},
        {"line_no": 2, "bad_code": "bar = 2", "guideline_ids": [], "memory_ids": []},
        {"line_no": 1, "bad_code": "baz = 3", "guideline_ids": [], "memory_ids": []},
        {"line_no": 2, "bad_code": "qux = 4", "guideline_ids": ["guideline-2"], "memory_ids": []},
    ]
    assert len(rr_blank.comments) == len(expected_comments)
    for comment, expected in zip(rr_blank.comments, expected_comments):
        assert comment.line_no == expected["line_no"]
        assert comment.bad_code == expected["bad_code"]
        assert comment.guideline_ids == expected["guideline_ids"]
