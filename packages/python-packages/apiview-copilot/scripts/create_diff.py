import difflib
import os
import sys
from typing import List, Optional


def create_diff(left: str, right: str, output_path: Optional[str] = None) -> str:
    """
    Create a Git-style diff between two files with line numbers prepended.

    Args:
        left: Path to the first file (old version)
        right: Path to the second file (new version)
        output_path: Optional path to write the diff to

    Returns:
        The diff as a string
    """

    # Read and number the files
    def prepend_line_numbers(file_path: str) -> List[str]:
        with open(file_path, "r", encoding="utf-8") as f:
            lines = f.readlines()
        return [f"{i+1}: {line.rstrip()}" for i, line in enumerate(lines)]

    try:
        left_lines = prepend_line_numbers(left)
        right_lines = prepend_line_numbers(right)
    except Exception as e:
        print(f"Error reading files: {e}")
        return ""

    # Get file names for the diff headers
    left_name = os.path.basename(left)
    right_name = os.path.basename(right)

    # Generate the unified diff
    diff = difflib.unified_diff(
        left_lines, right_lines, fromfile=f"a/{left_name}", tofile=f"b/{right_name}", lineterm=""
    )

    # Convert the diff to a string
    diff_text = "\n".join(diff)

    # Write to output file if specified
    if output_path:
        try:
            with open(output_path, "w", encoding="utf-8") as f:
                f.write(diff_text)
        except Exception as e:
            print(f"Error writing diff to {output_path}: {e}")

    return diff_text


if __name__ == "__main__":
    # Simple command line handling when run directly
    if len(sys.argv) < 3:
        print("Usage: python create_diff.py file1 file2 [output_file]")
        sys.exit(1)

    file1 = sys.argv[1]
    file2 = sys.argv[2]
    output = sys.argv[3] if len(sys.argv) > 3 else None

    diff = create_diff(file1, file2, output)

    # Print to stdout if no output file specified
    if not output:
        print(diff)
