# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import os
from .llc_view_models import LLCClientView, LLCOperationView, ParameterView

from .version import VERSION
from .parse_yml import parse_yaml, out_path

__all__ = [
        'LLCClientView',
        'LLCOperationView',
        'ParameterView',
        "Navigation",
        "NavigationTag",
        "Kind",
        "Diagnostic",
        "parse_yaml"
]


def console_entry_point():
    main_view = parse_yaml()
    # Write to JSON file
    out_path(main_view)