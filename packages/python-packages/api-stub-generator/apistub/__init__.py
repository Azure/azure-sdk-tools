import os
from ._version import VERSION
from ._stub_generator import StubGenerator
from ._token import Token
from ._token_kind import TokenKind
from ._apiview import ApiView, Navigation, NavigationTag, Kind
from ._diagnostic import Diagnostic, DiagnosticLevel

__version__ = VERSION

__all__ = [
    "StubGenerator",
    "Token",
    "TokenKind",
    "ApiView",
    "Navigation",
    "NavigationTag",
    "Kind",
    "Diagnostic",
    "DiagnosticLevel"
]


def console_entry_point():
    stub_generator = StubGenerator()

    if stub_generator.error_report:
        return stub_generator.generate_error_report()

    apiview = stub_generator.generate_tokens()
    json_tokens = stub_generator.serialize(apiview)
    # Write to JSON file
    out_file_path = stub_generator.out_path
    # Generate JSON file name if outpath doesn't have json file name
    if not out_file_path.endswith(".json"):
        out_file_path = os.path.join(
            stub_generator.out_path, "{0}_python.json".format(apiview.name)
        )
    with open(out_file_path, "w") as json_file:
        json_file.write(json_tokens)

    # fail the run if any errors are found
    err_count = apiview.report_errors(stub_generator)
    ret_code = 1 if err_count else 0
    return ret_code

