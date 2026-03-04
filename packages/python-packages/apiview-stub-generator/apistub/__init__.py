import os
import sys

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


def _get_output_paths(out_path, package_name):
    """Determine JSON output file path."""
    if out_path.endswith(".json"):
        return out_path
    return os.path.join(out_path, f"{package_name}_python.json")


def console_entry_point(_is_test=False):
    """Main entry point that handles file generation based on flags.

    Args:
        _is_test: Internal parameter used by tests to get the apiview object.
                 Should not be used by CLI callers.
    """
    print(f"Running apiview-stub-generator version {__version__}")
    stub_generator = StubGenerator()
    apiview = stub_generator.generate_tokens()

    json_path = _get_output_paths(stub_generator.out_path, apiview.package_name)

    # Always generate JSON
    json_tokens = stub_generator.serialize(apiview)
    with open(json_path, "w") as f:
        f.write(json_tokens)
    print(f"Generated JSON: {json_path}")

    # Return apiview only when explicitly requested by tests
    if _is_test:
        return apiview
