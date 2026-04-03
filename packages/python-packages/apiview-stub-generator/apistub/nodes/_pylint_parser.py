import inspect
import json
import logging
import os
import re
import subprocess
import sys
from types import SimpleNamespace
from typing import List, Union, TYPE_CHECKING

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

    @classmethod
    def _normalize_namespace_inits(cls, path):
        """Replace namespace azure/__init__.py files with empty files.

        Packages distributed as sdist or source include a namespace-package
        ``azure/__init__.py`` that extends ``__path__`` via ``pkgutil``.  When
        pylint / astroid sees that file it merges all installed azure sub-packages
        into a single namespace, which breaks decorator name resolution and causes
        the azure-sdk guidelines checker to silently produce no diagnostics.
        Overwriting those files with empty ones matches the behaviour of the WHL
        variant (which never carries a non-trivial ``azure/__init__.py``).

        Returns a dict mapping each overwritten path to its original content so
        callers can restore the files when done.
        """
        overwritten = {}  # init_path -> original content
        for root, dirs, files in os.walk(path):
            basename = os.path.basename(root)
            if basename == "azure" and "__init__.py" in files:
                init_path = os.path.join(root, "__init__.py")
                try:
                    with open(init_path, "r") as f:
                        content = f.read()
                    if "extend_path" in content or "pkgutil" in content:
                        overwritten[init_path] = content
                        with open(init_path, "w") as f:
                            pass  # overwrite with empty file
                except Exception:
                    pass
                # No need to recurse deeper once we've found azure/
                dirs.clear()
        return overwritten

    @classmethod
    def parse(cls, path):
        from apistub import ApiView

        # Replace namespace azure/__init__.py files so pylint resolves
        # decorators correctly regardless of distribution format (src/sdist/whl).
        # The originals are restored after pylint finishes (even on failure) to
        # avoid permanently mutating source checkouts.
        overwritten = cls._normalize_namespace_inits(path)

        pkg_name = os.path.split(path)[-1]
        rcfile_path = os.path.join(ApiView.get_root_path(), ".pylintrc")
        logging.debug(f"APIView root path: {ApiView.get_root_path()}")

        # Run pylint in a subprocess so that each analysis starts with a clean
        # Python/astroid state and is not affected by packages already imported
        # in the current process (e.g. during a full test-suite run).
        cmd = [sys.executable, "-m", "pylint", path, "-f", "json", "--recursive=y", "--rcfile", rcfile_path]
        try:
            result = subprocess.run(cmd, capture_output=True, text=True)
            try:
                raw_messages = json.loads(result.stdout or "[]")
            except json.JSONDecodeError:
                logging.warning(
                    "pylint produced non-JSON output for %s (exit code %s). stderr: %r stdout: %r",
                    path, result.returncode, result.stderr[:500], result.stdout[:200],
                )
                raw_messages = []
        finally:
            for init_path, content in overwritten.items():
                try:
                    with open(init_path, "w") as f:
                        f.write(content)
                except Exception:
                    logging.warning("Failed to restore %s after pylint run", init_path)

        # Wrap each JSON dict in a SimpleNamespace so PylintError can consume it
        # using the same attribute interface as a pylint Message object.
        messages = [
            SimpleNamespace(
                C=m.get("message-id", "?")[0],
                category=m.get("type", ""),
                module=m.get("module", ""),
                obj=m.get("obj", ""),
                line=m.get("line", 0),
                column=m.get("column", 0),
                end_line=m.get("endLine", None),
                end_column=m.get("endColumn", None),
                path=m.get("path", ""),
                symbol=m.get("symbol", ""),
                msg=m.get("message", ""),
                msg_id=m.get("message-id", ""),
            )
            for m in raw_messages
        ]

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

    @classmethod
    def match_items(cls, obj) -> None:
        try:
            source_file = inspect.getsourcefile(obj)
            (source_lines, start_line) = inspect.getsourcelines(obj)
            end_line = start_line + len(source_lines) - 1
        except Exception:
            return
        for item in cls.items:
            item_path = item.path
            if item_path and source_file.endswith(item_path):
                # nested items will overwrite the ownership of their
                # containing parent.
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
