"""TypeSpec library processor — converts .tsp files to structured markdown.

Parses TypeSpec definitions (models, operations, interfaces, enums, etc.)
and generates markdown documentation with code blocks for each definition.
"""

from __future__ import annotations

import logging
import os
import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Literal

logger = logging.getLogger(__name__)

DefinitionType = Literal[
    "model", "operation", "interface", "enum", "union",
    "alias", "namespace", "scalar", "decorator",
]


@dataclass
class TypeSpecDefinition:
    type: DefinitionType
    name: str
    full_name: str
    code: str
    decorators: list[str] = field(default_factory=list)
    description: str = ""
    comments: list[str] = field(default_factory=list)
    level: int = 1
    children: list[TypeSpecDefinition] | None = None


class TypeSpecProcessor:
    """Converts TypeSpec library .tsp files into markdown documentation."""

    def __init__(self, work_dir: str, relative_lib_dir: str) -> None:
        self._work_dir = work_dir
        self._relative_lib_dir = relative_lib_dir
        self._src_dir = os.path.join(work_dir, relative_lib_dir)
        self._dest_dir = os.path.join(work_dir, relative_lib_dir, "generated")

    def process_typespec_libraries(self) -> None:
        """Find all .tsp files and convert them to markdown."""
        if not os.path.isdir(self._src_dir):
            raise RuntimeError(f"TypeSpec library directory not found: {self._src_dir}")

        os.makedirs(self._dest_dir, exist_ok=True)

        # Collect all .tsp files
        tsp_files: list[str] = []
        for dirpath, _, filenames in os.walk(self._src_dir):
            for fname in filenames:
                if fname.endswith(".tsp"):
                    tsp_files.append(os.path.join(dirpath, fname))

        logger.info("Found %d .tsp files in %s", len(tsp_files), self._src_dir)

        for tsp_file in tsp_files:
            relative = os.path.relpath(tsp_file, self._src_dir)
            safe_name = relative.replace(os.sep, "#").replace("/", "#")
            md_file = os.path.join(self._dest_dir, safe_name.replace(".tsp", ".md"))
            self._convert_to_markdown(tsp_file, md_file)

    def _convert_to_markdown(self, tsp_file: str, md_file: str) -> None:
        """Convert a single TypeSpec file to markdown."""
        content = Path(tsp_file).read_text(encoding="utf-8")
        definitions = self._parse_definitions(content)
        relative = os.path.relpath(tsp_file, self._work_dir)
        markdown = self._generate_markdown(definitions)
        Path(md_file).write_text(markdown, encoding="utf-8")

    # --- Parser ---

    def _parse_definitions(self, content: str) -> list[TypeSpecDefinition]:
        lines = content.split("\n")
        return self._parse_definitions_from_lines(lines)

    def _parse_definitions_from_lines(
        self, lines: list[str], level: int = 1
    ) -> list[TypeSpecDefinition]:
        definitions: list[TypeSpecDefinition] = []
        current_def_start = -1
        current_body_start = -1
        current_type: DefinitionType | None = None
        current_name = ""
        current_level = level
        brace_count = 0
        has_global_namespace = False

        for i, line in enumerate(lines):
            trimmed = line.strip()
            match = self._match_definition_start(trimmed)

            if match and brace_count == 0:
                brace_count = trimmed.count("{") - trimmed.count("}")

                if match["type"] == "namespace" and trimmed.endswith(";"):
                    has_global_namespace = True
                    current_level += 1

                if current_def_start == -1:
                    # First definition — find its preamble start
                    current_def_start = self._find_preamble_start(lines, i)
                    current_body_start = i
                    current_type = match["type"]
                    current_name = match["name"]
                else:
                    # Emit the previous definition
                    prev_end = self._find_preamble_start_between(lines, current_def_start, i)
                    definition = self._build_definition(
                        current_type, current_name, lines,
                        current_def_start, current_body_start, i, current_level,
                    )
                    definitions.append(definition)

                    current_def_start = prev_end
                    current_body_start = i
                    current_type = match["type"]
                    current_name = match["name"]
            else:
                brace_count += trimmed.count("{") - trimmed.count("}")

        # Handle last definition
        if current_def_start != -1 and current_type and current_name:
            definition = self._build_definition(
                current_type, current_name, lines,
                current_def_start, current_body_start, len(lines), current_level,
            )
            definitions.append(definition)

        if has_global_namespace and definitions:
            definitions[0].level -= 1

        return definitions

    def _build_definition(
        self,
        def_type: DefinitionType,
        def_name: str,
        lines: list[str],
        def_start: int,
        body_start: int,
        next_body_start: int,
        level: int,
    ) -> TypeSpecDefinition:
        """Build a TypeSpecDefinition from line ranges."""
        # Find the actual end of this definition (before next definition's comments)
        def_end = self._find_definition_end(lines, def_start, next_body_start)

        # Extract comments and decorators from preamble
        comments: list[str] = []
        decorators: list[str] = []
        in_block_comment = False
        in_decorator = False
        paren_count = 0

        for n in range(def_start, body_start):
            trimmed = lines[n].strip()
            if trimmed.startswith("/**") and not in_block_comment:
                comments.append(trimmed)
                if not trimmed.endswith("*/"):
                    in_block_comment = True
                continue
            if in_block_comment:
                comments.append(trimmed)
                if trimmed.endswith("*/"):
                    in_block_comment = False
                continue
            if trimmed.startswith("//"):
                comments.append(trimmed)
                continue
            if trimmed.startswith("@") and not in_decorator:
                paren_count = trimmed.count("(") - trimmed.count(")")
                decorators.append(trimmed)
                in_decorator = True
                if trimmed.count("(") == 0 or (trimmed.endswith(")") and paren_count == 0):
                    in_decorator = False
                continue
            if in_decorator:
                decorators[-1] += "\n" + trimmed
                paren_count += trimmed.count("(") - trimmed.count(")")
                if trimmed.endswith(")") and paren_count == 0:
                    in_decorator = False

        # Parse children for namespaces and interfaces
        children: list[TypeSpecDefinition] = []
        if def_type == "namespace":
            children = self._parse_definitions_from_lines(
                lines[body_start + 1: def_end], level + 1
            )
        elif def_type == "interface":
            children = self._parse_interface_operations(
                lines, body_start, def_end, level + 1
            )

        description = self._extract_description(decorators, comments)

        return TypeSpecDefinition(
            type=def_type,
            name=def_name,
            full_name=def_name,
            code="\n".join(lines[def_start: def_end + 1]),
            decorators=decorators,
            description=description,
            comments=comments,
            level=level,
            children=children or None,
        )

    def _parse_interface_operations(
        self, lines: list[str], def_start: int, def_end: int, level: int
    ) -> list[TypeSpecDefinition]:
        """Parse operations from an interface body."""
        operations: list[TypeSpecDefinition] = []
        current_comments: list[str] = []
        current_decorators: list[str] = []
        op_lines: list[str] = []
        in_block_comment = False
        in_decorator = False
        in_operation = False
        brace_count = 0
        angle_count = 0
        paren_count = 0

        for i in range(def_start, def_end + 1):
            line = lines[i]
            trimmed = line.strip()

            if not trimmed and not in_operation and not in_block_comment and not in_decorator:
                continue

            # Block comments
            if trimmed.startswith("/**") and not in_block_comment:
                in_block_comment = True
                current_comments.append(trimmed)
                if trimmed.endswith("*/"):
                    in_block_comment = False
                continue
            if in_block_comment:
                current_comments.append(trimmed)
                if trimmed.endswith("*/"):
                    in_block_comment = False
                continue
            if trimmed.startswith("//"):
                current_comments.append(trimmed)
                continue

            # Decorators
            if trimmed.startswith("@") and not in_operation:
                in_decorator = True
                current_decorators.append(trimmed)
                paren_count = trimmed.count("(") - trimmed.count(")")
                if paren_count <= 0:
                    in_decorator = False
                continue
            if in_decorator:
                current_decorators[-1] += "\n" + trimmed
                paren_count += trimmed.count("(") - trimmed.count(")")
                if paren_count <= 0:
                    in_decorator = False
                continue

            # Operation detection
            op_match = (
                re.match(r"^op\s+(\w+)", trimmed)
                or re.match(r"^(\w+)\s+is\s+", trimmed)
                or re.match(r"^(\w+)\s*<", trimmed)
                or re.match(r"^(\w+)\s*\(", trimmed)
            )

            if op_match and not in_operation:
                in_operation = True
                op_lines = [line]
                brace_count = trimmed.count("{") - trimmed.count("}")
                angle_count = trimmed.count("<") - trimmed.count(">")
                paren_count = trimmed.count("(") - trimmed.count(")")

                if trimmed.endswith(";") and brace_count <= 0 and angle_count <= 0 and paren_count <= 0:
                    op = self._build_operation(
                        op_match.group(1), op_lines, current_decorators, current_comments, level
                    )
                    operations.append(op)
                    current_comments, current_decorators, op_lines = [], [], []
                    in_operation = False
                    brace_count = angle_count = paren_count = 0
                continue

            if in_operation:
                op_lines.append(line)
                brace_count += trimmed.count("{") - trimmed.count("}")
                angle_count += trimmed.count("<") - trimmed.count(">")
                paren_count += trimmed.count("(") - trimmed.count(")")

                if trimmed.endswith(";") and brace_count <= 0 and angle_count <= 0 and paren_count <= 0:
                    name = self._extract_op_name(op_lines[0])
                    op = self._build_operation(
                        name, op_lines, current_decorators, current_comments, level
                    )
                    operations.append(op)
                    current_comments, current_decorators, op_lines = [], [], []
                    in_operation = False
                    brace_count = angle_count = paren_count = 0

        return operations

    def _build_operation(
        self,
        name: str,
        op_lines: list[str],
        decorators: list[str],
        comments: list[str],
        level: int,
    ) -> TypeSpecDefinition:
        code = "\n".join([*comments, *decorators, *op_lines])
        description = self._extract_description(decorators, comments)
        return TypeSpecDefinition(
            type="operation",
            name=name,
            full_name=name,
            code=code,
            decorators=decorators,
            description=description,
            comments=comments,
            level=level,
        )

    @staticmethod
    def _extract_op_name(line: str) -> str:
        trimmed = line.strip()
        for pattern in [r"^op\s+(\w+)", r"^(\w+)\s+is\s+", r"^(\w+)\s*<", r"^(\w+)\s*\("]:
            m = re.match(pattern, trimmed)
            if m:
                return m.group(1)
        return "unknown"

    # --- Markdown generation ---

    def _generate_markdown(self, definitions: list[TypeSpecDefinition]) -> str:
        lines: list[str] = []
        for defn in definitions:
            self._emit_definition(defn, lines)
            if defn.children:
                for child in defn.children:
                    self._emit_definition(child, lines)
        return "\n".join(lines)

    @staticmethod
    def _emit_definition(defn: TypeSpecDefinition, output: list[str]) -> None:
        header = "#" * defn.level
        output.append(f"{header} {defn.full_name}")
        output.append("")
        output.append(f"**Type:** {defn.type.capitalize()}")
        output.append("")
        if defn.description:
            output.append(defn.description)
            output.append("")
        output.append("```typespec")
        output.append(defn.code)
        output.append("```")
        output.append("")
        output.append("")

    # --- Helpers ---

    @staticmethod
    def _match_definition_start(line: str) -> dict[str, str] | None:
        patterns: list[tuple[str, DefinitionType]] = [
            (r"^model\s+(\w+)", "model"),
            (r"^op\s+(\w+)", "operation"),
            (r"^interface\s+(\w+)", "interface"),
            (r"^enum\s+(\w+)", "enum"),
            (r"^union\s+(\w+)", "union"),
            (r"^alias\s+(\w+)", "alias"),
            (r"^namespace\s+([\w.]+)", "namespace"),
            (r"^scalar\s+(\w+)", "scalar"),
            (r"^(?:extern\s+)?dec\s+(\w+)", "decorator"),
        ]
        for pattern, def_type in patterns:
            m = re.match(pattern, line)
            if m:
                return {"type": def_type, "name": m.group(1)}
        return None

    @staticmethod
    def _find_preamble_start(lines: list[str], current: int) -> int:
        """Walk backward from current to find where comments/decorators start."""
        in_comment = False
        for i in range(current - 1, -1, -1):
            trimmed = lines[i].strip()
            if trimmed.endswith("*/"):
                in_comment = True
            if trimmed.startswith("/*"):
                in_comment = False
            is_comment = in_comment or trimmed.startswith("//")
            if not is_comment and (trimmed.endswith(";") or trimmed.endswith("}")):
                return i + 1
        return 0

    @staticmethod
    def _find_preamble_start_between(lines: list[str], start: int, current: int) -> int:
        """Find start of next definition's preamble looking backward from current."""
        in_comment = False
        for i in range(current - 1, start, -1):
            trimmed = lines[i].strip()
            if trimmed.endswith("*/"):
                in_comment = True
            if trimmed.startswith("/*"):
                in_comment = False
            is_comment = in_comment or trimmed.startswith("//")
            if not is_comment and (trimmed.endswith(";") or trimmed.endswith("}")):
                return i + 1
        return start

    @staticmethod
    def _find_definition_end(lines: list[str], start: int, next_start: int) -> int:
        """Find end of current definition (before next definition's comments)."""
        in_comment = False
        for i in range(next_start - 1, start, -1):
            trimmed = lines[i].strip()
            if trimmed.endswith("*/"):
                in_comment = True
            if trimmed.startswith("/*"):
                in_comment = False
            is_comment = in_comment or trimmed.startswith("//")
            if not is_comment and (trimmed.endswith(";") or trimmed.endswith("}")):
                return i
        return min(next_start - 1, len(lines) - 1)

    @staticmethod
    def _extract_description(decorators: list[str], comments: list[str]) -> str:
        """Extract description from @doc decorator or JSDoc comments."""
        # Try @doc decorator
        for dec in decorators:
            m = re.match(r'^@doc\s*\(\s*"([^"]*)"\s*\)', dec)
            if m:
                return m.group(1)
            m = re.match(r'^@doc\s*\(\s*"""([\s\S]*?)"""\s*\)', dec)
            if m:
                return m.group(1).strip()
            m = re.match(r'^@summary\s*\(\s*"([^"]*)"\s*\)', dec)
            if m:
                return m.group(1)

        # Try JSDoc comments
        if comments:
            return TypeSpecProcessor._parse_jsdoc(comments)
        return ""

    @staticmethod
    def _parse_jsdoc(comments: list[str]) -> str:
        """Parse JSDoc comments, excluding @param and other tags."""
        desc_lines: list[str] = []
        in_tag = False
        tag_prefixes = (
            "@param", "@template", "@returns", "@return", "@example",
            "@see", "@deprecated", "@throws", "@type", "@typedef",
            "@callback", "@property", "@prop", "@arg", "@argument",
        )

        for line in comments:
            clean = line
            clean = re.sub(r"^/\*\*?\s*", "", clean)
            clean = re.sub(r"\s*\*/$", "", clean)
            clean = re.sub(r"^\*\s?", "", clean)
            clean = re.sub(r"^//\s?", "", clean)
            trimmed = clean.strip()

            if any(trimmed.startswith(t) for t in tag_prefixes):
                in_tag = True
                continue
            if trimmed.startswith("@"):
                in_tag = True
                continue
            if in_tag:
                if not trimmed or re.match(r"^[A-Z]", trimmed):
                    in_tag = False
                else:
                    continue
            if trimmed:
                desc_lines.append(trimmed)

        return " ".join(desc_lines).strip()
