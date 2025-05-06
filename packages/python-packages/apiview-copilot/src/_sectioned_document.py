import re
from typing import List, Optional


class LineData:
    def __init__(self, *, line_no: Optional[int], indent: int, line: str, git_status: Optional[str] = None):
        self.line_no = line_no
        self.indent = indent
        self.line = line
        self.git_status = git_status


class Section:

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

    def __init__(
        self,
        *,
        lines: List[str] = None,
        line_data: List[LineData] = None,
        base_indent: int = 0,
        max_chunk_size: int = 500,
    ):
        self.sections = []
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
            if x.indent == base_indent and x.line[base_indent:] != "" and x.line[base_indent:] != "}"
        ]

        # Handle case with no top-level lines
        if not top_level_lines:
            self.sections.append(Section(line_data))
            return

        # Create initial sections based on top-level lines
        initial_sections = []
        for i in range(len(top_level_lines)):
            try:
                line1 = top_level_lines[i]
                line2 = top_level_lines[i + 1]
                line1_idx = line_data.index(line1)
                line2_idx = line_data.index(line2)
                lines_between = line_data[line1_idx:line2_idx]
            except IndexError:
                # Last section, take all remaining lines
                line1_idx = line_data.index(line1)
                lines_between = line_data[line1_idx:]
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

                # top line of the oversized section
                top_line = section.lines[0]

                # place the remaining lines in a new sectioned documents
                sub_sections = SectionedDocument(
                    line_data=section.lines[1:], base_indent=base_indent + 1, max_chunk_size=max_chunk_size
                )
                for sub_section in sub_sections:
                    # Add the top line of the oversized section to the new sub-section
                    sub_section.lines.insert(0, top_line)
                    # Add the sub-sections as new sections
                    self.sections.append(sub_section)
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
