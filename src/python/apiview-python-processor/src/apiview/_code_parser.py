#!/usr/bin/env python

# --------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.
# --------------------------------------------------------------------------------------------

import glob
import sys
import os
import argparse
import logging
import inspect
import ast
import io
import importlib
import astroid
import docstring_parser

from _namespace_node import NameSpaceNode

INIT_PY_FILE = "__init__.py"


def parse(root_path):
    """This method returns a dictionary of namespace and all public classes in each namespace
    """
    dict_namespaces = {}
    azure_root_path = os.path.join(root_path, "azure")

    for root, _, files in os.walk(azure_root_path):
        if INIT_PY_FILE in files:
            module_path = root.replace(root_path, "")
            name_space = module_path.replace(os.path.sep, ".")[1:]
            module_obj = importlib.import_module(name_space)
            dict_namespaces[name_space] = NameSpaceNode(name_space, module_obj)
            dict_namespaces[name_space].dump()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="parses code")

    parser.add_argument(
        "--pkg-path", required=True, help=("Package root path"),
    )

    args = parser.parse_args()
    parse(args.pkg_path)
