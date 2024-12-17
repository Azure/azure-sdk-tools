from enum import Enum


class DiagnosticLevel(int, Enum):
    DEFAULT = 0
    INFO = 1
    WARNING = 2
    ERROR = 3

    def __str__(self):
        if self == 0:
            return "DEFAULT"
        elif self == 1:
            return "INFO"
        elif self == 2:
            return "WARNING"
        elif self == 3:
            return "ERROR"


class Diagnostic:
    id_counter = 1

    def __init__(self, *, obj: "PylintError", target_id: str):
        diagnostic_number = Diagnostic.id_counter
        self.diagnostic_id = f"AZ_PY_{diagnostic_number}"
        Diagnostic.id_counter += 1
        self.text = f"{obj.message} [{obj.symbol}]"
        self.help_link_uri = obj.help_link
        self.target_id = target_id
        self.level = obj.level

    def log(self, log_func):
        log_func(f"{str(self.level)}: {self.target_id}: {self.text}")
