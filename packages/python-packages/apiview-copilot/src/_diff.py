# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Module for creating unified diffs with line numbers.
"""

import difflib
import re


def create_diff_with_line_numbers(*, old: str, new: str) -> str:
    """
    Create a unified diff between two files using difflib, with line numbers prepended.

    For unchanged and added (+) lines, prepends the line number in the "new" file.
    For removed (-) lines, prepends the line number in the "old" file.

    Args:
        old: Text of the first file (old version)
        new: Text of the second file (new version)

    Returns:
        The diff as a string with line numbers prepended
    """
    # Generate the diff using difflib
    diff_text = create_diff(old, new)
    if not diff_text:
        return ""

    # Process the diff to add line numbers
    numbered_diff = []

    # Process the diff line by line
    left_line_no = 0
    right_line_no = 0
    in_hunk = False

    for line in diff_text.splitlines():
        # Handle hunk headers (@@ -a,b +c,d @@)
        if line.startswith("@@"):
            in_hunk = True
            # Extract line numbers from hunk header
            match = re.match(r"^@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@", line)
            if match:
                left_line_no = int(match.group(1)) - 1  # 0-indexed for processing
                right_line_no = int(match.group(2)) - 1  # 0-indexed for processing
            numbered_diff.append(line)
            continue

        if not in_hunk:
            numbered_diff.append(line)
            continue

        # Process diff content with line numbers
        if line.startswith("-"):
            left_line_no += 1
            # For removed lines, use the left file line number
            numbered_diff.append(f"{left_line_no}: {line}")
        elif line.startswith("+"):
            right_line_no += 1
            # For added lines, use the right file line number
            numbered_diff.append(f"{right_line_no}: {line}")
        else:
            # For context lines, increment both and use the right file line number
            left_line_no += 1
            right_line_no += 1
            numbered_diff.append(f"{right_line_no}: {line}")

    result = "\n".join(numbered_diff)
    return result


def create_diff(old_content: str, new_content: str) -> str:
    """
    Create a unified diff between two text contents using difflib.

    Args:
        old_content: Text of the first file (old version)
        new_content: Text of the second file (new version)

    Returns:
        The diff as a string
    """
    # Generate the unified diff
    diff = difflib.unified_diff(
        old_content.splitlines(),
        new_content.splitlines(),
        fromfile="a/old_content",
        tofile="b/new_content",
        lineterm="",  # Avoid adding extra newlines
    )
    return "\n".join(diff)
