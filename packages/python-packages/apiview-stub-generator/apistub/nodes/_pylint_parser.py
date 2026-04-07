import inspect
import json
import logging
import os
from sys import stderr
import re
from typing import Dict, List, Union, TYPE_CHECKING
from pylint.lint import Run

if TYPE_CHECKING:
    from ._base_node import NodeEntityBase

_HELP_LINK_REGEX = re.compile(r"(.+) See details: *([^\s]+)")


class PylintError:

    def __init__(self, pkg_name, msg):
        from apistub import DiagnosticLevel

        self.code = msg.C
        self.category = msg.category
        self.module = msg.module
        self.obj = msg.obj
        self.line = msg.line
        self.column = msg.column
        self.end_line = msg.end_line
        self.end_column = msg.end_column
        self.path = msg.path.split(os.path.sep)[-2:] if msg.path else None
        self.symbol = msg.symbol
        self.message = msg.msg
        self.message_id = msg.msg_id
        self.help_link = None
        code = self.symbol[0] if self.symbol else ""
        if self.path:
            self.path = os.path.join(*self.path)
        self.level = DiagnosticLevel.ERROR if code in "EF" else DiagnosticLevel.WARNING
        self.owner = None
        self._parse_help_link()

    def _parse_help_link(self):
        try:
            (message, help_link) = _HELP_LINK_REGEX.findall(self.message)[0]
            self.message = message
            self.help_link = help_link
        except Exception as err:
            # if unable to parse, leave alone
            return

    def generate_tokens(self, apiview, target_id):
        apiview.add_diagnostic(err=self, target_id=target_id)


class PylintParser:

    AZURE_CHECKER_CODE = "47"

    items: List[PylintError] = []
    _path_to_items: Dict[str, List[PylintError]] = {}

    @classmethod
    def parse(cls, path):
        from apistub import ApiView

        pkg_name = os.path.split(path)[-1]
        rcfile_path = os.path.join(ApiView.get_root_path(), ".pylintrc")
        logging.debug(f"APIView root path: {ApiView.get_root_path()}")
        params = [path, "-f", "json", "--recursive=y", f"--rcfile={rcfile_path}", "--ignore=tests,build,samples,examples,doc"]
        messages = Run(params, exit=False).linter.reporter.messages
        plugin_failed = any([x.symbol == "bad-plugin-value" for x in messages])
        if plugin_failed:
            logging.error(
                f"Unable to load pylint_guidelines_checker. Check that it is installed."
            )
        cls.items = [
            PylintError(pkg_name, x)
            for x in messages
            if x.msg_id[1:3] == PylintParser.AZURE_CHECKER_CODE
        ]
        # Build a path-keyed index so match_items can skip items in other files
        # without iterating over the full list for every node.
        cls._path_to_items = {}
        for item in cls.items:
            if item.path:
                cls._path_to_items.setdefault(item.path, []).append(item)

    @classmethod
    def match_items(cls, obj) -> None:
        if not cls.items:
            return
        try:
            source_file = inspect.getsourcefile(obj)
            if not source_file:
                return
        except Exception:
            return

        # Find the subset of pylint errors that belong to this source file.
        # Iterating over unique path keys (typically O(tens)) is much cheaper
        # than scanning all items for every node.
        candidates = None
        for path_key, path_items in cls._path_to_items.items():
            if source_file.endswith(path_key):
                candidates = path_items
                break
        if not candidates:
            return

        try:
            if inspect.isclass(obj):
                # Avoid inspect.getsourcelines for classes — it triggers an
                # O(N_lines) ast.parse + AST walk (Python's _ClassFinder).
                # Use our pre-built file index instead.  The lazy import is
                # safe: _class_node is always fully loaded before any node
                # __init__ runs, so there is no circular-import issue at
                # runtime even though the static import chain would be circular.
                from apistub.nodes._class_node import _get_class_line_range  # noqa: PLC0415
                start_line, end_line = _get_class_line_range(obj)
                if start_line is None:
                    return
            else:
                (source_lines, start_line) = inspect.getsourcelines(obj)
                end_line = start_line + len(source_lines) - 1
        except Exception:
            return

        for item in candidates:
            # nested items will overwrite ownership of their containing parent.
            if item.line >= start_line and item.line <= end_line:
                item.owner = str(obj)

    @classmethod
    def get_items(cls, node: Union["NodeEntityBase", str]) -> List[PylintError]:
        if isinstance(node, str): # "GLOBAL"
            items = [x for x in cls.items if x.owner == str(node)]
        else:
            items = [x for x in cls.items if node.is_pylint_error_owner(x)]
        return items

    @classmethod
    def get_unclaimed(cls) -> List[PylintError]:
        return [x for x in cls.items if not x.owner]
