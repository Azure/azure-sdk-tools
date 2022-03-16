from enum import Enum


_HELP_URL_STRING = "https://github.com/Azure/azure-sdk-tools/blob/main/doc/python_apiview_errors.md#{0}"




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

    SUPPORTED_DIAGNOSTIC_CODES = {
        "missing-type",
        "missing-source-link",
        "missing-kwargs",
        "name-mismatch",
        "decorator-parse",
        "missing-return-type",
        "missing-typehint",
        "return-type-mismatch",
        "list-return-type"
    }

    def __init__(self, *, code, target_id, message, level):
        if code not in Diagnostic.SUPPORTED_DIAGNOSTIC_CODES:
            raise ValueError(f"APIView found unexpected code '{code}'.")
        self.diagnostic_id = "AZ_PY_{}".format(Diagnostic.id_counter)
        Diagnostic.id_counter += 1
        self._code = code
        self.text = f"[{code}] {message}"
        self.help_link_uri = _HELP_URL_STRING.format(code)
        self.target_id = target_id
        self.level = level

    def log(self, log_func):
        log_func(f"{str(self.level)}: {self.target_id}: {self.text}")
