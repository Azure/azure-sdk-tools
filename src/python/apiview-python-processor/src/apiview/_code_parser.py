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


INIT_PY_FILE = "__init__.py"


class NodeEntity:
    def __init__(self, obj):
        super().__init__()
        self.obj = obj
        self.name = obj.__qualname__
        self.display_name = self.name
        self.annotations = []
        self.child_nodes = []
        self._inspect()


    def _inspect(self):
        if inspect.isclass(self.obj):
            self._inspect_class()
        elif inspect.isfunction(self.obj) or inspect.ismethod(self.obj):
            self._inspect_function()


    def _inspect_class(self):
        self.display_name = "class {}".format(self.name)
        # find members in node 
        self.child_nodes = [
            NodeEntity(childObj)
            for name, childObj in inspect.getmembers(self.obj) if not name.startswith("_")
            if inspect.isclass(childObj) or inspect.isfunction(childObj) or inspect.ismethod(childObj)
        ]
        

    def _inspect_function(self):
        sig = str(inspect.signature(self.obj))
        self.display_name = "def {0}{1}".format(self.display_name, sig)
        node = astroid.extract_node(inspect.getsource(self.obj))
        if node.decorators:
            self.annotations = [x.name for x in node.decorators.nodes if hasattr(x, "name")]

        #print(self.display_name)
        #print(self.display_name)
        #print("argsspec")
        #print(inspect.getfullargspec(self.obj))


    def get_display_name(self):
        return self.display_name    


    def dump(self):
        print("     {}\n".format(self.display_name))
        for n in self.child_nodes:
            for annotation in n.annotations:
                print("         @{}".format(annotation))
            print("         {}\n".format(n.display_name))


class NameSpaceDef:
    def __init__(self, namespace):
        super().__init__()
        self.namespace = namespace
        self.child_nodes = []
        self._inspect(namespace)


    def _inspect(self, namespace):
        """Imports module given as namespace, identify public entities in module and inspect them recursively
        """
        self.module = importlib.import_module(namespace)
        if hasattr(self.module, "__all__"):
            public_entities = getattr(self.module, "__all__")
            self.child_nodes = [
                NodeEntity(obj)
                for name, obj in inspect.getmembers(self.module)
                if name in public_entities
            ]


    def get_children(self):
        return self.child_nodes

    """ def _import_public_entities(self):

        module_dict = self.module.__dict__
        if self.public_entities:
            globals().update({name: module_dict[name] for name in self.public_entities})

    def inspect_members(self):

        self._import_public_entities()
        module_dict = self.module.__dict__
        for mem in self.public_entities:
            #print("****{}*****".format(mem))
            for entity_name, entity_data in inspect.getmembers(module_dict[mem]):
                if entity_name.startswith("__"):
                    continue """
    # print("{0}: {1}".format(entity_name, entity_data))

    def dump(self):
        if self.child_nodes:
            print("{}".format(self.namespace))
            for e in self.child_nodes:
                e.dump()


def find_published_classes(root_path):
    """This method returns a dictionary of namespace and all public classes in each namespace
    """
    dict_namespaces = {}
    azure_root_path = os.path.join(root_path, "azure")

    for root, _, files in os.walk(azure_root_path):
        if INIT_PY_FILE in files:
            module_path = root.replace(root_path, "")
            name_space = module_path.replace(os.path.sep, ".")[1:]
            dict_namespaces[name_space] = NameSpaceDef(name_space)
            dict_namespaces[name_space].dump()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="parses code")

    parser.add_argument(
        "--pkg-path", required=True, help=("Package root path"),
    )

    args = parser.parse_args()
    find_published_classes(args.pkg_path)
