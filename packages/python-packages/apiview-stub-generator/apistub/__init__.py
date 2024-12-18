import os

from ._version import VERSION
from ._stub_generator import StubGenerator
from ._generated.treestyle.parser.models import (
    CodeDiagnostic as Diagnostic,
    CodeDiagnosticLevel as DiagnosticLevel,
    TokenKind,
    ReviewToken as Token,
)
from ._generated.treestyle.parser.models._patch import ApiView, ReviewLine, ReviewLines

__version__ = VERSION

__all__ = [
    "StubGenerator",
    "Token",
    "TokenKind",
    "ApiView",
    "ReviewLine",
    "ReviewLines",
    "Diagnostic",
    "DiagnosticLevel",
]


def console_entry_point():
    print("Running apiview-stub-generator version {}".format(__version__))
    stub_generator = StubGenerator()
    apiview = stub_generator.generate_tokens()
    json_tokens = stub_generator.serialize(apiview)
    # Write to JSON file
    out_file_path = stub_generator.out_path
    # Generate JSON file name if outpath doesn't have json file name
    if not out_file_path.endswith(".json"):
        out_file_path = os.path.join(stub_generator.out_path, "{0}_python.json".format(apiview.package_name))
    with open(out_file_path, "w") as json_file:
        json_file.write(json_tokens)
