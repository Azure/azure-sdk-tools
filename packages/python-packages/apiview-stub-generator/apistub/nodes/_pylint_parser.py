import inspect
import json
import logging
import os
from sys import stderr
import re
from typing import List
from pylint.lint import Run

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
    def parse(cls, path):
        from apistub import ApiView

        pkg_name = os.path.split(path)[-1]
        rcfile_path = os.path.join(ApiView.get_root_path(), ".pylintrc")
        logging.debug(f"APIView root path: {ApiView.get_root_path()}")
        params = f"{path} -f json --recursive=y --rcfile {rcfile_path}".split(" ")
        messages = Run(params, exit=False).linter.reporter.messages
        plugin_failed = any([x.symbol == "bad-plugin-value" for x in messages])
        if plugin_failed:
            logging.error(f"Unable to load pylint_guidelines_checker. Check that it is installed.")
        cls.items = [PylintError(pkg_name, x) for x in messages if x.msg_id[1:3] == PylintParser.AZURE_CHECKER_CODE]

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
    def get_items(cls, obj) -> List[PylintError]:
        items = [x for x in cls.items if x.owner == str(obj)]
        return items

    @classmethod
    def get_unclaimed(cls) -> List[PylintError]:
        return [x for x in cls.items if not x.owner]
