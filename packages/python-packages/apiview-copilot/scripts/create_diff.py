import os
import sys
import subprocess
from typing import Optional


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
            f"--src-prefix=old/{left_name}:",  # Custom prefix for old file
            f"--dst-prefix=new/{right_name}:",  # Custom prefix for new file
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
        print("Usage: python create_diff.py file1 file2 [output_file]")
        sys.exit(1)

    file1 = sys.argv[1]
    file2 = sys.argv[2]
    output = sys.argv[3] if len(sys.argv) > 3 else None

    diff = create_diff(file1, file2, output)

    # Print to stdout if no output file specified
    if not output:
        print(diff)
