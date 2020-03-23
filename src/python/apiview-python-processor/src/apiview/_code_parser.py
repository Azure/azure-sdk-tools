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
import json

from nodes._namespace_node import NameSpaceNode
from _apiview import ApiView, APIViewEncoder, Navigation, Kind, NavigationTag

INIT_PY_FILE = "__init__.py"

class NodeIndex:
    index = {}

    def add(self, name, node):
        if name in self.index:
            raise ValueError("Index already has {} node".format(name))
        self.index[name] = node


    def get(self, name):
        return self.index.get(name, None)


    def get_id(self, name):
        node = self.get(name)
        if node and hasattr(node, "namespace_id"):
            return node.namespace_id
        return None


def parse(root_path):
    """This method returns a dictionary of namespace and all public classes in each namespace
    """
    dict_namespaces = {}
    azure_root_path = os.path.join(root_path, "azure")
    nodeindex = NodeIndex()
    apiview = ApiView(nodeindex, "azure-storage-blob",0, "0.0.1")
    

    for root, subdirs, files in os.walk(azure_root_path):
        # Ignore any modules with name starts with "_"
        # For e.g. _generated, _shared etc
        dirs_to_skip = [x for x in subdirs if x.startswith("_")]
        for d in dirs_to_skip:
            subdirs.remove(d)

        if INIT_PY_FILE in files:
            module_path = root.replace(root_path, "")
            name_space = module_path.replace(os.path.sep, ".")[1:]
            module_obj = importlib.import_module(name_space)
            dict_namespaces[name_space] = NameSpaceNode(name_space, module_obj, nodeindex)
    
    # Generate tokens
    navigation = Navigation("azure_storage_blob.whl", None)
    navigation.set_tag(NavigationTag(Kind.type_package))
    apiview.add_navigation(navigation)

    namespaces = dict_namespaces.keys()
    for n in namespaces:
        dict_namespaces[n].generate_tokens(apiview)
        namespace_nav = dict_namespaces[n].get_navigation()
        #dict_namespaces[n].dump()
        if namespace_nav:
            navigation.add_child(namespace_nav)

    # Serialize apiview to json    
    json_apiview = APIViewEncoder().encode(apiview)
    json_file_name = "C:\\packages\\azure_storage_blob_python_test.json"
    with open(json_file_name, "w") as json_file:
        json_file.write(json_apiview)


def extract_wheel(whl_path):
    pass


def json_generator(obj):
    try:
        return obj.toJSON()
    except:
        return obj.__dict__


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="parses code")

    parser.add_argument(
        "--pkg-path", required=True, help=("Package root path"),
    )

    args = parser.parse_args()

    parse(args.pkg_path)
