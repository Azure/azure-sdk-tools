# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

import json
import shutil
import subprocess
import tempfile
from pathlib import Path
from typing import List

from apistub import ApiView

# Path to Export-APIViewMarkdown.ps1, resolved relative to this file so it
# works both locally and in CI without any configuration.
#   tests/_test_util.py
#     → tests/
#       → apiview-stub-generator/
#         → python-packages/
#           → packages/
#             → <repo root>/eng/common/scripts/
_REPO_ROOT = Path(__file__).resolve().parents[4]
_EXPORT_SCRIPT = _REPO_ROOT / "eng" / "common" / "scripts" / "Export-APIViewMarkdown.ps1"


def _tokenize(node):
    apiview = ApiView(pkg_name="test", namespace="test")
    node.generate_tokens(apiview.review_lines)
    return apiview.review_lines


""" Returns the review line tokens rendered into distinct lines. """


def _render_lines(review_lines) -> List[str]:
    lines = review_lines.render()
    return [x for x in lines]


""" Returns the review line tokens as a single concatenated string. """


def _render_string(review_lines) -> str:
    lines = _render_lines(review_lines)
    return _merge_lines(lines)


""" Merges the provided lines together, removing any leading whitespace."""


def _merge_lines(lines) -> str:
    return "".join([x for x in lines])


def _check(actual, expected, client):
    assert len(actual) == len(expected), f"\n*******\nClient: {client.__name__}\nActual:   {actual}\nExpected: {expected}\n*******"
    for i in range(len(expected)):
        assert (
            actual[i] == expected[i]
        ), f"\n*******\nClient: {client.__name__}\nActual:   {actual[i]}\nExpected: {expected[i]}\n*******"

MockApiView = ApiView(pkg_name="test", namespace="test")

def _count_review_line_metadata(tokens, metadata):
    lastRelatedTo = None
    for token in tokens:
        if lastRelatedTo:
            assert token["LineId"] == lastRelatedTo
            lastRelatedTo = None
        # count the number of relatedToLines
        if "RelatedToLine" in token:
            metadata["RelatedToLine"] += 1
            # Only check the next token LineId if current line is not an empty line.
            if len(token["Tokens"]) > 0:
                lastRelatedTo = token["RelatedToLine"]
        if "IsContextEndLine" in token and token["IsContextEndLine"]:
            metadata["IsContextEndLine"] += 1
        if "Children" in token:
            _count_review_line_metadata(token["Children"], metadata)

    return metadata


def render_api_view_markdown(apiview) -> str:
    """Serialize *apiview* to a temp JSON file, run Export-APIViewMarkdown.ps1 via
    ``pwsh``, and return the resulting markdown string.

    Skips (via ``pytest.skip``) when:
    * ``pwsh`` is not on PATH (CI agents that lack PowerShell Core)
    * the Export-APIViewMarkdown.ps1 script cannot be found

    This intentionally delegates all rendering logic to the PS1 script so that
    the tests stay in sync automatically when the script changes.
    """
    import pytest  # local import – only needed for tests

    if not shutil.which("pwsh"):
        pytest.skip("pwsh not available – skipping api.md comparison")

    if not _EXPORT_SCRIPT.exists():
        pytest.skip(f"Export-APIViewMarkdown.ps1 not found at {_EXPORT_SCRIPT}")

    from apistub._stub_generator import APIViewEncoder  # noqa: PLC0415

    with tempfile.TemporaryDirectory() as tmp:
        json_path = Path(tmp) / "tokens.json"
        md_path = Path(tmp) / "api.md"

        json_path.write_text(APIViewEncoder().encode(apiview), encoding="utf-8")

        result = subprocess.run(
            [
                "pwsh",
                "-NoProfile",
                "-NonInteractive",
                "-File",
                str(_EXPORT_SCRIPT),
                "-TokenJsonPath",
                str(json_path),
                "-OutputPath",
                str(md_path),
            ],
            capture_output=True,
            text=True,
        )
        if result.returncode != 0:
            raise RuntimeError(
                f"Export-APIViewMarkdown.ps1 failed (exit {result.returncode}):\n"
                f"{result.stderr or result.stdout}"
            )

        return md_path.read_text(encoding="utf-8")
