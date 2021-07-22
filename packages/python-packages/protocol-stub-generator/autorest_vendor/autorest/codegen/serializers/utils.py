# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
from typing import List


def serialize_method(
    *,
    function_def: str,
    method_name: str,
    is_in_class: bool,
    method_param_signatures: List[str],
):
    lines: List[str] = []
    lines.append(f"{function_def} {method_name}(")
    if is_in_class:
        lines.append("    self,")
    lines.extend([
        ("    " + line)
        for line in method_param_signatures
    ])
    lines.append(")")
    return "\n".join(lines)

def method_signature_and_response_type_annotation_template(
    *,
    async_mode: bool,
    method_signature: str,
    response_type_annotation: str,
) -> str:
    if async_mode:
        return f"{method_signature} -> {response_type_annotation}:"
    return f"{method_signature}:\n    # type: (...) -> {response_type_annotation}"
