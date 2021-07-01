# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from .protocol_models import LLCClientView, LLCOperationView, LLCParameterView,LLCOperationGroupView
from .parse_yml import LLCGenerator

__all__ = [
        'LLCClientView',
        'LLCOperationView',
        'LLCParameterView',
        "LLCOperationGroupView",
        "LLCGenerator"
]


def console_entry_point():
        llc_gen = LLCGenerator()
        llc_gen.parse_yaml()
    