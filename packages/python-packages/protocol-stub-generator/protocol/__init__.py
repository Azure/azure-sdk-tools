# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from .protocol_models import (
    ProtocolClientView,
    ProtocolOperationView,
    ProtocolParameterView,
    ProtocolOperationGroupView,
)
from .parse_yml import LLCGenerator

__all__ = [
    "ProtocolClientView",
    "ProtocolOperationView",
    "ProtocolParameterView",
    "ProtocolOperationGroupView",
    "LLCGenerator",
]


def console_entry_point():
    llc_gen = LLCGenerator()
    llc_gen.parse_yaml()
