import os
import sys

from ._version import VERSION
from ._stub_generator import StubGenerator
from ._markdown_renderer import render_markdown
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
    
    # Determine output file paths
    out_file_path = stub_generator.out_path
    if not out_file_path.endswith(".json"):
        json_file_path = os.path.join(
            stub_generator.out_path, "{0}_python.json".format(apiview.package_name)
        )
        md_file_path = os.path.join(
            stub_generator.out_path, "api.md"
        )
    else:
        json_file_path = out_file_path
        md_file_path = os.path.join(
            os.path.dirname(out_file_path), "api.md"
        )
    
    # Generate JSON if requested
    if stub_generator.generate_json:
        json_tokens = stub_generator.serialize(apiview)
        with open(json_file_path, "w") as json_file:
            json_file.write(json_tokens)
        print("Generated JSON: {}".format(json_file_path))
    
    # Generate markdown if requested
    if stub_generator.generate_md:
        md_content = render_markdown(apiview)
        with open(md_file_path, "w") as md_file:
            md_file.write(md_content)
        print("Generated markdown: {}".format(md_file_path))
