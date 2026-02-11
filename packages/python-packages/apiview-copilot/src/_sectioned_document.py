# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Module for sectioning large documents with line numbers.
"""

import re
from typing import List, Optional


def _is_decorator_line(line: str) -> bool:
    """Check if a line is a decorator (starts with @ after stripping whitespace)."""
    stripped = line.lstrip()
    return stripped.startswith("@")


class LineData:
    """Data class representing a line in the document with its metadata."""

    def __init__(self, *, line_no: Optional[int], indent: int, line: str, git_status: Optional[str] = None):
        self.line_no = line_no
        self.indent = indent
        self.line = line
        self.git_status = git_status


class Section:
    """Represents a section of the document with its lines and metadata."""

    def __init__(self, lines: List[LineData]):
        self.lines = lines

    def start_line_no(self) -> int:
        """Returns the line number of the first line in the section."""
        return self.lines[0].line_no

    def numbered(self) -> str:
        """Returns the lines with line numbers."""
        numbered_lines = []
        for x in self.lines:
            val = ""
            if x.line_no is not None:
                val = f"{x.line_no}: {x.git_status or ''}"
            numbered_lines.append(f"{val}{x.line}")
        return "\n".join(numbered_lines)

    def idx_for_line_no(self, line_no: int) -> int:
        """Returns the index of the line with the given line number."""
        for i, line in enumerate(self.lines):
            if line.line_no == line_no:
                return i
        return None

    def __str__(self):
        return "\n".join([x.line for x in self.lines])


class SectionedDocument:
    """Represents a document sectioned into smaller parts based on indentation and line numbers."""

    def __init__(
        self,
        *,
        lines: List[str] = None,
        line_data: List[LineData] = None,
        base_indent: int = 0,
        max_chunk_size: int = 500,
    ):
        if max_chunk_size == 1:
            raise ValueError("max_chunk_size must be greater than 1")

        self.sections: List[Section] = []
        # Step 1: Create initial fine-grained sections based on indentation
        if line_data is None:
            line_data = []
            for line in lines:
                # Parse the line to get it's line number and diff status
                pattern = r"^(\d+): ( |\+|\-)?(.*)$"
                match = re.match(pattern, line)
                if match:
                    line_no = int(match.group(1))
                    git_status = match.group(2)
                    line = match.group(3)
                else:
                    line_no = None
                    git_status = None
                indent = len(line) - len(line.lstrip())
                line_data.append(LineData(line_no=line_no, indent=indent, line=line, git_status=git_status))

        top_level_lines = [
            x
            for x in line_data
            if x.indent == base_indent
            and x.line[base_indent:] != ""
            and x.line[base_indent:] != "}"
            and not _is_decorator_line(x.line)  # Skip decorators - they'll be grouped with next non-decorator
        ]

        # Handle case with no top-level lines
        if not top_level_lines:
            self.sections.append(Section(line_data))
            return

        # Create initial sections based on top-level lines
        # Each section includes any preceding decorator lines at the same indent level
        initial_sections = []
        for i, line1 in enumerate(top_level_lines):
            line1_idx = line_data.index(line1)

            # Look backward to find any preceding decorator lines that belong to this section
            section_start_idx = line1_idx
            while section_start_idx > 0:
                prev_line = line_data[section_start_idx - 1]
                # Include preceding decorators at the same indent level
                if prev_line.indent == base_indent and _is_decorator_line(prev_line.line):
                    section_start_idx -= 1
                else:
                    break

            try:
                line2 = top_level_lines[i + 1]
                line2_idx = line_data.index(line2)
                # Look backward from line2 to find where its decorators start
                # (so we don't include them in this section)
                section_end_idx = line2_idx
                while section_end_idx > line1_idx:
                    prev_line = line_data[section_end_idx - 1]
                    if prev_line.indent == base_indent and _is_decorator_line(prev_line.line):
                        section_end_idx -= 1
                    else:
                        break
                lines_between = line_data[section_start_idx:section_end_idx]
            except IndexError:
                # Last section, take all remaining lines
                lines_between = line_data[section_start_idx:]
            initial_sections.append(Section(lines_between))

        # Step 2: Combine small sections into larger ones up to max_chunk_size
        current_section_lines = []
        current_size = 0

        for section in initial_sections:
            section_size = len(section.lines)

            # Handle oversized single sections - subdivide them
            if section_size > max_chunk_size:
                # If we have an accumulated chunk, add it first
                if current_size > 0:
                    self.sections.append(Section(current_section_lines))
                    current_section_lines = []
                    current_size = 0

                # Subdivide the oversized section
                # Find all header lines (leading decorators + first non-decorator declaration)
                # These should be included in all subsections
                header_lines = []
                body_start_idx = 0
                for idx, ld in enumerate(section.lines):
                    if _is_decorator_line(ld.line):
                        header_lines.append(ld)
                    else:
                        # First non-decorator line is the declaration - include it in header
                        header_lines.append(ld)
                        body_start_idx = idx + 1
                        break

                # Defensive handling: if the section contains only decorator lines,
                # treat them as header-only with no body to avoid duplication.
                if body_start_idx == 0 and header_lines and all(_is_decorator_line(ld.line) for ld in header_lines):
                    body_start_idx = len(section.lines)

                header_size = len(header_lines)
                body_lines = section.lines[body_start_idx:]

                if not body_lines:
                    # No body lines, just add the header as a single section
                    self.sections.append(Section(header_lines.copy()))
                else:
                    # Calculate how many body lines can fit per chunk.
                    # Note: if header_size >= max_chunk_size (e.g. many decorators), body_per_chunk
                    # is clamped to 1 so each chunk will exceed max_chunk_size. This is intentional
                    # since headers must always be included for context.
                    body_per_chunk = max_chunk_size - header_size
                    if body_per_chunk < 1:
                        body_per_chunk = 1

                    for i in range(0, len(body_lines), body_per_chunk):
                        chunk = header_lines.copy() + body_lines[i : i + body_per_chunk]
                        self.sections.append(Section(chunk))
                continue

            # If adding this section would exceed max_chunk_size, finalize current chunk
            if current_size > 0 and (current_size + section_size) > max_chunk_size:
                self.sections.append(Section(current_section_lines))
                current_section_lines = section.lines.copy()
                current_size = section_size
            else:
                # First section or still under max size, add to current chunk
                current_section_lines.extend(section.lines)
                current_size += section_size

        # Add the last chunk if there's anything left
        if current_section_lines:
            self.sections.append(Section(current_section_lines))

    def __iter__(self):
        return iter(self.sections)

    def __len__(self):
        # Return the number of sections in the document
        return len(self.sections)

    def numbered(self) -> str:
        """Returns the sections with line numbers."""
        numbered_sections = []
        for section in self.sections:
            numbered_sections.append(section.numbered())
        value = "\n\n".join(numbered_sections)
        return value
