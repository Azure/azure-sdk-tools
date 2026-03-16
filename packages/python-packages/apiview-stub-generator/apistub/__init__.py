import os
import subprocess

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
    """Determine JSON and markdown output file paths."""
    if out_path.endswith(".json"):
        json_path = out_path
        md_path = os.path.join(os.path.dirname(out_path), "api.md")
    else:
        json_path = os.path.join(out_path, f"{package_name}_python.json")
        md_path = os.path.join(out_path, "api.md")
    return json_path, md_path


def _export_markdown(json_path, md_path):
    """Invoke the bundled Export-APIViewMarkdown.ps1 script to convert JSON to markdown."""
    script_path = os.path.join(os.path.dirname(__file__), "scripts", "Export-APIViewMarkdown.ps1")
    subprocess.run(
        ["pwsh", script_path, "-TokenJsonPath", json_path, "-OutputPath", md_path],
        check=True,
    )


def console_entry_point():
    print("Running apiview-stub-generator version {}".format(__version__))
    stub_generator = StubGenerator()
    apiview = stub_generator.generate_tokens()

    json_path, md_path = _get_output_paths(stub_generator.out_path, apiview.package_name)

    # Always generate JSON
    json_tokens = stub_generator.serialize(apiview)
    with open(json_path, "w") as f:
        f.write(json_tokens)
    print(f"Generated JSON: {json_path}")

    # Optionally generate markdown if --md flag is specified
    if stub_generator.md:
        _export_markdown(json_path, md_path)
        print(f"Generated markdown: {md_path}")
