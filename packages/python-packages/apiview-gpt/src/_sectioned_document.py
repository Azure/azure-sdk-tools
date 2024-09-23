from typing import List, NamedTuple

class LineData(NamedTuple):
    line_no: int
    indent: int
    line: str


class Section:

    def __init__(self, lines: List[str], start_line_no: int):
        self.lines = lines
        self.start_line_no = start_line_no

    def __str__(self):
        return "\n".join(self.lines)


class SectionedDocument:

    def __init__(self, lines: List[str], chunk: bool):
        self.sections = []

        if chunk:
            line_data = []
            for i, line in enumerate(lines):
                indent = len(line) - len(line.lstrip())
                line_data.append(LineData(i, indent, line))

            top_level_lines = [x for x in line_data if x.indent == 0 and x.line != "" and x.line != "}"]
            for i in range(len(top_level_lines)):
                line1 = top_level_lines[i]
                try:
                    line2 = top_level_lines[i + 1]
                    lines_between = line_data[line1.line_no : line2.line_no]
                except IndexError:
                    lines_between = line_data[line1.line_no:]
                section = Section([x.line for x in lines_between], line1.line_no)
                self.sections.append(section)
        else:
            # just do one big chunk
            self.sections.append(Section(lines, 0))
