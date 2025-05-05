import os
import re
import subprocess
import tempfile
from typing import Optional


def create_diff_with_line_numbers(*, old: str, new: str) -> str:
    """
    Create a Git-style diff between two files using git diff command, with line numbers prepended.

    For unchanged and added (+) lines, prepends the line number in the "new" file.
    For removed (-) lines, prepends the line number in the "old" file.

    Args:
        old: Text of the first file (old version)
        new: Text of the second file (new version)

    Returns:
        The diff as a string with line numbers prepended
    """
    # First, get the regular diff
    diff_text = create_diff(old, new)
    if not diff_text:
        return ""

    # Now process the diff to add line numbers
    numbered_diff = []

    # Process the diff line by line
    left_line_no = 0
    right_line_no = 0
    in_hunk = False

    for line in diff_text.splitlines():
        # Handle diff header lines
        if line.startswith("diff ") or line.startswith("index ") or line.startswith("--- ") or line.startswith("+++ "):
            numbered_diff.append(line)
            continue

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
    Create a Git-style diff between two text contents using git diff command.

    Args:
        old_content: Text of the first file (old version)
        new_content: Text of the second file (new version)

    Returns:
        The diff as a string
    """
    try:
        # Create temporary files
        with tempfile.NamedTemporaryFile(mode="w+", encoding="utf-8", delete=False) as left_file:
            left_file.write(old_content)
            left_path = left_file.name

        with tempfile.NamedTemporaryFile(mode="w+", encoding="utf-8", delete=False) as right_file:
            right_file.write(new_content)
            right_path = right_file.name

        # Run git diff with the desired options
        cmd = [
            "git",
            "diff",
            "--no-index",  # Compare files without requiring them to be in a git repo
            "--color=never",  # No ANSI color codes
            "--diff-algorithm=histogram",  # Use histogram diff algorithm
            "-U0",
            "-W",
            "--",
            left_path,
            right_path,
        ]

        # Run the command and capture output
        result = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)

        # Remove temporary files
        os.unlink(left_path)
        os.unlink(right_path)

        # git diff returns exit code 1 if files differ, which is expected
        if result.returncode > 1:
            print(f"Error running git diff: {result.stderr}")
            return ""

        # Clean up the diff output - replace temporary file paths with nicer labels
        diff_text = result.stdout
        diff_text = diff_text.replace(f"a/{os.path.basename(left_path)}", "a/old_content")
        diff_text = diff_text.replace(f"b/{os.path.basename(right_path)}", "b/new_content")

        return diff_text

    except Exception as e:
        print(f"Error creating diff: {e}")
        return ""
