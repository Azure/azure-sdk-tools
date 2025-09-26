# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Test cases for verifying that diff-based prompts focus only on changed lines.
"""

from src._sectioned_document import LineData, Section


def test_section_numbered_with_diff_markers():
    """Test that Section.numbered() correctly includes git diff markers."""
    lines = [
        LineData(line_no=1, indent=0, line="unchanged_line_1", git_status=None),
        LineData(line_no=2, indent=0, line="removed_line", git_status="-"),
        LineData(line_no=3, indent=0, line="added_line", git_status="+"),
        LineData(line_no=4, indent=0, line="unchanged_line_2", git_status=None),
    ]
    sec = Section(lines)
    expected = "\n".join([
        "1: unchanged_line_1",
        "2: -removed_line",
        "3: +added_line",
        "4: unchanged_line_2",
    ])
    assert sec.numbered() == expected


def test_section_numbered_identifies_changed_lines_only():
    """Test that we can identify which lines should be targeted for comments."""
    lines = [
        LineData(line_no=1, indent=0, line="class MyClass:", git_status=None),
        LineData(line_no=2, indent=4, line="def old_method(self):", git_status="-"),
        LineData(line_no=3, indent=4, line="def new_method(self):", git_status="+"),
        LineData(line_no=4, indent=8, line="pass", git_status=None),
    ]
    sec = Section(lines)
    
    # Check the numbered output shows diff markers correctly
    numbered_output = sec.numbered()
    lines_to_comment = []
    
    for line in numbered_output.split("\n"):
        # Lines with + should be commented on, lines with - or no marker should not
        if ": +" in line:
            lines_to_comment.append(line)
    
    # Only the added line should be identified for commenting
    assert len(lines_to_comment) == 1
    assert "def new_method(self):" in lines_to_comment[0]


def test_section_handles_no_diff_markers():
    """Test that Section.numbered() works with no diff markers (regular review mode)."""
    lines = [
        LineData(line_no=1, indent=0, line="class MyClass:", git_status=None),
        LineData(line_no=2, indent=4, line="def method(self):", git_status=None),
    ]
    sec = Section(lines)
    expected = "\n".join([
        "1: class MyClass:",
        "2: def method(self):",
    ])
    assert sec.numbered() == expected