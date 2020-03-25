#!/usr/bin/env python

# --------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.
# --------------------------------------------------------------------------------------------

import glob
import sys
import os
import argparse
import astroid
import inspect
import io
import importlib
import json
import logging
import shutil
from subprocess import check_call
import zipfile


from ._apiview import ApiView, APIViewEncoder, Navigation, Kind, NavigationTag

INIT_PY_FILE = "__init__.py"

logging.getLogger().setLevel(logging.INFO)

class NodeIndex:
    """Maintains name to navigation ID"""
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

class StubGenerator:

    def _find_modules(self, pkg_root_path):
        """Find modules within the package to import and parse
        :param str: pkg_root_path
            Package root path
        :rtype: list
        """
        modules = []
        azure_root_path = os.path.join(pkg_root_path, "azure")
        for root, subdirs, files in os.walk(azure_root_path):
            # Ignore any modules with name starts with "_"
            # For e.g. _generated, _shared etc
            dirs_to_skip = [x for x in subdirs if x.startswith("_")]
            for d in dirs_to_skip:
                subdirs.remove(d)

            if INIT_PY_FILE in files:
                module_path = root.replace(pkg_root_path, "")
                module_name= module_path.replace(os.path.sep, ".")[1:]
                modules.append(module_name)
        return modules


    def _generate_tokens(self, pkg_root_path, package_name, version):
        """This method returns a dictionary of namespace and all public classes in each namespace
        """
        # Import ModuleNode since it needs NodeIndex.
        # Importing it globaly can cause circular dependency
        from apistub.nodes._module_node import ModuleNode
        module_dict = {}
        nodeindex = NodeIndex()
        # todo (Update the version number correctly)
        apiview = ApiView(nodeindex, package_name,0, version)
        modules = self._find_modules(pkg_root_path)
        logging.info("Modules to generate tokens: {}".format(modules))
        # load all modules and parse them recursively
        for m in modules:
            logging.info("Importing module {}".format(m))
            module_obj = importlib.import_module(m)
            module_dict[m] = ModuleNode(m, module_obj, nodeindex)
        
        # Create navigation info to navigate within APIreview tool
        navigation = Navigation(package_name, None)
        navigation.set_tag(NavigationTag(Kind.type_package))
        apiview.add_navigation(navigation)

        # Generate tokens
        modules = module_dict.keys()
        for m in modules:
            # Generate and add token to APIView
            logging.info("Generating tokens for module {}".format(m))
            module_dict[m].generate_tokens(apiview)
            # Add navigation info for this modules. navigation info is used to build tree panel in API tool
            module_nav = module_dict[m].get_navigation()
            if module_nav:
                navigation.add_child(module_nav)
        return apiview


    def _serialize_tokens(self, apiview, out_file_path):
        """Serialize tokens into json file
        :param ApiView: apiview
        """
        # Serialize apiview to json
        logging.info("Serializing tokens into json")
        json_apiview = APIViewEncoder().encode(apiview)
        with open(out_file_path, "w") as json_file:
            json_file.write(json_apiview)
        logging.info("Serialized tokens into json file [{}]".format(out_file_path))


    def _extract_wheel(self):
        """Extract the wheel into out dir and return root path to azure root directory in package
        """
        file_name, _ = os.path.splitext(os.path.basename(self.pkg_path))
        temp_pkg_dir = os.path.join(self.temp_path, file_name)
        if os.path.exists(temp_pkg_dir):
            logging.info("Cleaning up existing temp directory: {}".format(temp_pkg_dir))
            shutil.rmtree(temp_pkg_dir)
        os.mkdir(temp_pkg_dir)

        logging.info("Extracting {0} to directory {1}".format(self.pkg_path, temp_pkg_dir))
        zip_file = zipfile.ZipFile(self.pkg_path)
        zip_file.extractall(temp_pkg_dir)
        logging.info("Extracted package files into temp path")
        return temp_pkg_dir


    def _parse_pkg_name(self):
        file_name = os.path.basename(self.pkg_path)
        whl_name, extn = os.path.splitext(file_name)
        if extn[1:] not in ['whl', 'zip']:
            raise ValueError("Invalid type of package. API view parser expects wheel or sdist package")

        filename_parts = whl_name.split("-")
        pkg_name = filename_parts[0].replace("_", "-")
        version = filename_parts[1]
        return pkg_name, version


    def _install_package(self):
        # Force install the package to parse so inspect can get members in package
        commands = [sys.executable, "-m", "pip", "install", "--force-reinstall", self.pkg_path]
        check_call(commands)


    def generate_tokens(self):
        # Extract package to temp directory
        logging.info("Extracting package to temp path")
        pkg_root_path = self._extract_wheel()
        pkg_name, version = self._parse_pkg_name()
        logging.info("package name: {0}, version:{1}".format(pkg_name, version))

        logging.info("Installing package from {}".format(self.pkg_path))
        self._install_package()
        logging.info("Generating tokens")
        apiview = self._generate_tokens(pkg_root_path, pkg_name, version)

        json_out_file_name = "{0}_{1}.json".format(pkg_name, version)
        out_file_path = os.path.join(pkg_root_path, json_out_file_name)
        logging.info("Generated tokens. Serializing tokens into JSON")
        self._serialize_tokens(apiview, out_file_path)
        logging.info("Completed parsing package and generating tokens")

    
    def __init__(self):
        parser = argparse.ArgumentParser(description="Parse a python package and generate json token file to be supplied to API review tool")
        parser.add_argument(
            "--pkg-path", required=True, help=("Package root path"),
        )
        parser.add_argument(
            "--temp-path", required=True, help=("Temp path to extract package"),
        )
        parser.add_argument(
            "--out-path", required=True, help=("Path to generate json file with parsed tokens"),
        )

        args = parser.parse_args()
        if not os.path.exists(args.pkg_path):
            logging.error("Package path [{}] is invalid".format(args.pkg_path))
            exit(1)
        elif not os.path.exists(args.temp_path):
            logging.error("Temp path [{0}] is invalid".format(args.temp_path))
            exit(1)        
        elif not os.path.exists(args.out_path):
            logging.error("Output path [{}] is invalid".format(args.out_path))
            exit(1)

        self.pkg_path = args.pkg_path
        self.temp_path = args.temp_path
        self.out_path = args.out_path
        
