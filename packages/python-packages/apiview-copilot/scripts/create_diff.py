import os
import sys
import re
from typing import Optional

# Add the parent directory to the Python path so we can import from src
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

# Now import from src
from src._diff import create_diff as diff_from_content
from src._diff import create_diff_with_line_numbers as diff_with_line_numbers


def create_diff_with_line_numbers(left_path: str, right_path: str, output_path: Optional[str] = None) -> str:
    """
    Create a Git-style diff between two files with line numbers prepended.

    For unchanged and added (+) lines, prepends the line number in the "new" file.
    For removed (-) lines, prepends the line number in the "old" file.

    Args:
        left_path: Path to the first file (old version)
        right_path: Path to the second file (new version)
        output_path: Optional path to write the diff to

    Returns:
        The diff as a string with line numbers prepended
    """
    # Read both files
    try:
        with open(left_path, "r", encoding="utf-8") as f:
            left_content = f.read()
        with open(right_path, "r", encoding="utf-8") as f:
            right_content = f.read()
    except Exception as e:
        print(f"Error reading files: {e}")
        return ""

    # Use the imported function from _diff.py to get the numbered diff
    result = diff_with_line_numbers(left_content, right_content)

    # Write to output file if specified
    if output_path and result:
        try:
            with open(output_path, "w", encoding="utf-8") as f:
                f.write(result)
        except Exception as e:
            print(f"Error writing numbered diff to {output_path}: {e}")

    return result


def create_diff(left_path: str, right_path: str, output_path: Optional[str] = None) -> str:
    """
    Create a Git-style diff between two files.

    Args:
        left_path: Path to the first file (old version)
        right_path: Path to the second file (new version)
        output_path: Optional path to write the diff to

    Returns:
        The diff as a string
    """
    try:
        # Read both files
        with open(left_path, "r", encoding="utf-8") as f:
            left_content = f.read()
        with open(right_path, "r", encoding="utf-8") as f:
            right_content = f.read()

        # Use the imported function from _diff.py
        diff_text = diff_from_content(left_content, right_content)

        # Write to output file if specified
        if output_path and diff_text:
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
