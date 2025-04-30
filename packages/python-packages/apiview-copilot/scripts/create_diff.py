import os
import sys
import subprocess
import re
from typing import Optional, Tuple, List


def create_diff_with_line_numbers(left: str, right: str, output_path: Optional[str] = None) -> str:
    """
    Create a Git-style diff between two files using git diff command, with line numbers prepended.

    For unchanged and added (+) lines, prepends the line number in the "new" file.
    For removed (-) lines, prepends the line number in the "old" file.

    Args:
        left: Path to the first file (old version)
        right: Path to the second file (new version)
        output_path: Optional path to write the diff to

    Returns:
        The diff as a string with line numbers prepended
    """
    # First, get the regular diff
    diff_text = create_diff(left, right)
    if not diff_text:
        return ""

    # Now process the diff to add line numbers
    numbered_diff = []

    # Read both files to track line numbers
    with open(left, "r", encoding="utf-8") as f:
        left_lines = f.readlines()
    with open(right, "r", encoding="utf-8") as f:
        right_lines = f.readlines()

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

    # Write to output file if specified
    if output_path:
        try:
            with open(output_path, "w", encoding="utf-8") as f:
                f.write(result)
        except Exception as e:
            print(f"Error writing numbered diff to {output_path}: {e}")

    return result


def create_diff(left: str, right: str, output_path: Optional[str] = None) -> str:
    """
    Create a Git-style diff between two files using git diff command.

    Args:
        left: Path to the first file (old version)
        right: Path to the second file (new version)
        output_path: Optional path to write the diff to

    Returns:
        The diff as a string
    """
    try:
        # Get absolute paths
        left_abs = os.path.abspath(left)
        right_abs = os.path.abspath(right)

        # Check files exist
        if not os.path.exists(left_abs):
            print(f"Error: File not found: {left}")
            return ""
        if not os.path.exists(right_abs):
            print(f"Error: File not found: {right}")
            return ""

        # Get file names for display in diff
        left_name = os.path.basename(left)
        right_name = os.path.basename(right)

        # Run git diff with the desired options
        cmd = [
            "git",
            "diff",
            "--no-index",  # Compare files without requiring them to be in a git repo
            "--color=never",  # No ANSI color codes
            "--diff-algorithm=histogram",  # Use histogram diff algorithm
            "-U0",
            "-W",
            f"--src-prefix=a/{left_name}:",  # Custom prefix for old file
            f"--dst-prefix=b/{right_name}:",  # Custom prefix for new file
            "--",
            left_abs,
            right_abs,
        ]

        # Run the command and capture output
        result = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)

        # git diff returns exit code 1 if files differ, which is expected
        if result.returncode > 1:
            print(f"Error running git diff: {result.stderr}")
            return ""

        diff_text = result.stdout

        # Write to output file if specified
        if output_path:
            try:
                with open(output_path, "w", encoding="utf-8") as f:
                    f.write(diff_text)
            except Exception as e:
                print(f"Error writing diff to {output_path}: {e}")

        return diff_text

    except Exception as e:
        print(f"Error creating diff: {e}")
        return ""


if __name__ == "__main__":
    # Simple command line handling when run directly
    if len(sys.argv) < 3:
        print("Usage: python create_diff.py file1 file2 [output_file] [--numbered]")
        sys.exit(1)

    file1 = sys.argv[1]
    file2 = sys.argv[2]
    output = None
    use_line_numbers = False

    # Parse remaining args
    for arg in sys.argv[3:]:
        if arg == "--numbered":
            use_line_numbers = True
        elif not output:  # First non-flag argument is the output file
            output = arg

    # Generate diff based on options
    if use_line_numbers:
        diff = create_diff_with_line_numbers(file1, file2, output)
    else:
        diff = create_diff(file1, file2, output)

    # Print to stdout if no output file specified
    if not output:
        print(diff)
