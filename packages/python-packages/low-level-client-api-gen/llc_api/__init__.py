# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import os
from .llc_view_models import LLCClientView, LLCOperationView, LLCParameterView

from .version import VERSION
from .parse_yml import parse_yaml, out_path

__all__ = [
        'LLCClientView',
        'LLCOperationView',
        'LLCParameterView',
        "Navigation",
        "NavigationTag",
        "Kind",
        "Diagnostic",
        "parse_yaml",
        "out_path"
]


def console_entry_point():
    main_view = parse_yaml()
    # Write to JSON file
    out_path(main_view)