#!/usr/bin/env python

# --------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.
# --------------------------------------------------------------------------------------------
from argparse import ArgumentParser
import os
import tempfile

def parse_args():
    parser = ArgumentParser(
        description="Parse a python package and generate json token file to be supplied to API review tool"
    )
    parser.add_argument(
        "--pkg-path", required=True, help=("Package root path"),
    )
    parser.add_argument(
        "--temp-path",
        help=("Temp path to extract package"),
        default=tempfile.gettempdir(),
    )
    parser.add_argument(
        "--out-path",
        default=os.getcwd(),
        help=("Path to generate json file with parsed tokens"),
    )
    parser.add_argument(
        "--verbose",
        help=("Enable verbose logging"),
        default=False,
        action="store_true",
    )

    parser.add_argument(
        "--hide-report",
        help=("Hide diagnostic report"),
        default=False,
        action="store_true",
    )

    parser.add_argument(
        "--filter-namespace",
        help=("Generate Api view only for a specific namespace"),
    )

    return parser.parse_args()