import inspect
import json
import logging
import os
from sys import stderr
from pylint import epylint
import re
from typing import List

_HELP_LINK_REGEX = re.compile(r"(.+) See details: *([^\s]+)")

class PylintError:

    def __init__(self, pkg_name, **kwargs):
        from apistub import DiagnosticLevel
        self.type = kwargs.pop('type', None)
        self.module = kwargs.pop('module', None)
        self.obj = kwargs.pop('obj', None)
        self.line = kwargs.pop('line', None)
        self.column = kwargs.pop('column', None)
        self.end_line = kwargs.pop('endLine', None)
        self.end_column = kwargs.pop('endColumn', None)
        self.path = kwargs.pop('path', None)
        self.symbol = kwargs.pop('symbol', None)
        self.message = kwargs.pop('message', None)
        self.message_id = kwargs.pop('message-id', None)
        self.help_link = None
        if self.path and self.path.startswith(pkg_name):
            self.path = self.path[(len(f"{pkg_name}\\\\") - 1):]
        code = self.symbol[0] if self.symbol else ""
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
        apiview.add_diagnostic(obj=self, target_id=target_id)


class PylintParser:

    AZURE_CHECKER_CODE = "47"

    items: List[PylintError] = []

    @classmethod
    def parse(cls, path):
        from apistub import ApiView
        pkg_name = os.path.split(path)[-1]
        rcfile_path = os.path.join(ApiView.get_root_path(), "pylintrc")
        logging.debug(f"APIView root path: {ApiView.get_root_path()}")
        (pylint_stdout, pylint_stderr) = epylint.py_run(f"{path} -f json --recursive=y --rcfile {rcfile_path}", return_std=True)
        stderr_str = pylint_stderr.read()
        # strip put stray, non-json lines from stdout
        stdout_lines = [x for x in pylint_stdout.readlines() if not x.startswith("Exception")]
        try:
            json_items = json.loads("".join(stdout_lines))
            plugin_failed = any([x["symbol"] == "bad-plugin-value" for x in json_items])
            if plugin_failed:
                logging.error(f"Unable to load pylint_guidelines_checker. Check that it is installed.")
            cls.items = [PylintError(pkg_name, **x) for x in json_items if x["message-id"][1:3] == PylintParser.AZURE_CHECKER_CODE]
        except Exception as err:
            from apistub import DiagnosticLevel
            logging.error(f"Error decoding pylint output:\n{stderr_str}")
            logging.error(f"Error content:\n{err}")
            logging.error(f"==STDOUT==\n{stdout_lines}\n==END STDOUT==")
            # instead of raising an error, we will log a pylint error
            error = PylintError(pkg_name)
            error.level = DiagnosticLevel.ERROR
            error.owner = "GLOBAL"
            error.symbol = "apiview-pylint-parse-error"
            error.message = "Failure parsing pylint output. Please post an issue in the `Azure/azure-sdk-tools` repository."
            cls.items = [error]

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
