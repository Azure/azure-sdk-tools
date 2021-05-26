# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import os
import json
from .llc_view_models import LLCClientView, LLCOperationView, LLCParameterView,LLCOperationGroupView

from .version import VERSION
from .parse_yml import llc_generator

__all__ = [
        'LLCClientView',
        'LLCOperationView',
        'LLCParameterView',
        "LLCOperationGroupView",
        "Navigation",
        "NavigationTag",
        "Kind",
        "Diagnostic",
        "parse_yaml",
        "out_path"
]


def console_entry_point():
        llc_gen = llc_generator()
        llc_gen.parse_yaml()
    