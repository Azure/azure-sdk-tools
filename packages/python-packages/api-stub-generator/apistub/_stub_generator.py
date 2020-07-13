#!/usr/bin/env python

# --------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.
# --------------------------------------------------------------------------------------------

import glob
import sys
import os
import argparse

import inspect
import io
import importlib
import json
import logging
import pkgutil
import shutil
import ast
import textwrap
import io
import re
import typing
import tempfile
from subprocess import check_call
import zipfile


from apistub._apiview import ApiView, APIViewEncoder, Navigation, Kind, NavigationTag

INIT_PY_FILE = "__init__.py"

logging.getLogger().setLevel(logging.ERROR)


class StubGenerator:
    def __init__(self):
        parser = argparse.ArgumentParser(
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

        args = parser.parse_args()
        if not os.path.exists(args.pkg_path):
            logging.error("Package path [{}] is invalid".format(args.pkg_path))
            exit(1)
        elif not os.path.exists(args.temp_path):
            logging.error("Temp path [{0}] is invalid".format(args.temp_path))
            exit(1)


        self.pkg_path = args.pkg_path
        self.temp_path = args.temp_path
        self.out_path = args.out_path
        self.hide_report = args.hide_report
        if args.verbose:
            logging.getLogger().setLevel(logging.DEBUG)

    def generate_tokens(self):
        # Extract package to temp directory if it is wheel or sdist
        if self.pkg_path.endswith(".whl") or self.pkg_path.endswith(".zip"):
            logging.info("Extracting package to temp path")
            pkg_root_path = self._extract_wheel()
            pkg_name, version, namespace = self._parse_pkg_name()
        else:
            # package root is passed as arg to parse
            pkg_root_path = self.pkg_path
            pkg_name, version, namespace = parse_setup_py(pkg_root_path)

        logging.debug("package name: {0}, version:{1}, namespace:{2}".format(pkg_name, version, namespace))

        logging.debug("Installing package from {}".format(self.pkg_path))
        self._install_package(pkg_name)
        logging.debug("Generating tokens")
        apiview = self._generate_tokens(pkg_root_path, pkg_name, version, namespace)
        if apiview.Diagnostics:
            # Show error report in console
            if not self.hide_report:
                print("************************** Error Report **************************")
                for m in self.module_dict.keys():
                    self.module_dict[m].print_errors()
            logging.info("*************** Completed parsing package with errors ***************")
        else:
            logging.info("*************** Completed parsing package and generating tokens ***************")
        return apiview


    def serialize(self, apiview, encoder=APIViewEncoder):
        # Serialize tokens into JSON
        logging.debug("Serializing tokens into json")
        json_apiview = encoder().encode(apiview)
        return json_apiview


    def _find_modules(self, pkg_root_path):
        """Find modules within the package to import and parse
        :param str: pkg_root_path
            Package root path
        :rtype: list
        """
        modules = []
        for root, subdirs, files in os.walk(pkg_root_path):
            # Ignore any modules with name starts with "_"
            # For e.g. _generated, _shared etc
            dirs_to_skip = [x for x in subdirs if x.startswith("_") or x.startswith(".")]
            for d in dirs_to_skip:
                subdirs.remove(d)

            # Add current path as module name if _init.py is present
            if INIT_PY_FILE in files:
                module_name = os.path.relpath(root, pkg_root_path).replace(
                    os.path.sep, "."
                )
                modules.append(module_name)
                # Add any public py file names as modules
                sub_modules = [
                    os.path.splitext(os.path.basename(f))[0]
                    for f in files
                    if f.endswith(".py") and not os.path.basename(f).startswith("_")
                ]
                modules.extend(["{0}.{1}".format(module_name, x) for x in sub_modules])

        logging.debug("Modules in package: {}".format(modules))
        return modules


    def _generate_tokens(self, pkg_root_path, package_name, version, namespace):
        """This method returns a dictionary of namespace and all public classes in each namespace
        """
        # Import ModuleNode.
        # Importing it globally can cause circular dependency since it needs NodeIndex that is defined in this file
        from apistub.nodes._module_node import ModuleNode

        self.module_dict = {}
        nodeindex = NodeIndex()
        # todo (Update the version number correctly)
        apiview = ApiView(nodeindex, package_name, 0, version, namespace)
        modules = self._find_modules(pkg_root_path)
        logging.debug("Modules to generate tokens: {}".format(modules))

        # find root module name
        root_module = ""
        if namespace:
            root_module = namespace.split(".")[0]

        # load all modules and parse them recursively
        for m in modules:
            if not m.startswith(root_module):
                logging.debug("Skipping module {0}. Module should start with {1}".format(m, root_module))
                continue

            logging.debug("Importing module {}".format(m))
            module_obj = importlib.import_module(m)
            self.module_dict[m] = ModuleNode(m, module_obj, nodeindex)

        # Create navigation info to navigate within APIreview tool
        navigation = Navigation(package_name, None)
        navigation.set_tag(NavigationTag(Kind.type_package))
        apiview.add_navigation(navigation)

        # Generate tokens
        modules = self.module_dict.keys()
        for m in modules:
            # Generate and add token to APIView
            logging.debug("Generating tokens for module {}".format(m))
            self.module_dict[m].generate_tokens(apiview)
            # Add navigation info for this modules. navigation info is used to build tree panel in API tool
            module_nav = self.module_dict[m].get_navigation()
            if module_nav:
                navigation.add_child(module_nav)
        return apiview

    def _extract_wheel(self):
        """Extract the wheel into out dir and return root path to azure root directory in package
        """
        file_name, _ = os.path.splitext(os.path.basename(self.pkg_path))
        temp_pkg_dir = os.path.join(self.temp_path, file_name)
        if os.path.exists(temp_pkg_dir):
            logging.debug(
                "Cleaning up existing temp directory: {}".format(temp_pkg_dir)
            )
            shutil.rmtree(temp_pkg_dir)
        os.mkdir(temp_pkg_dir)

        logging.debug(
            "Extracting {0} to directory {1}".format(self.pkg_path, temp_pkg_dir)
        )
        zip_file = zipfile.ZipFile(self.pkg_path)
        zip_file.extractall(temp_pkg_dir)
        logging.debug("Extracted package files into temp path")
        return temp_pkg_dir

    def _parse_pkg_name(self):
        file_name = os.path.basename(self.pkg_path)
        whl_name, extn = os.path.splitext(file_name)
        if extn[1:] not in ["whl", "zip"]:
            raise ValueError(
                "Invalid type of package. API view parser expects wheel or sdist package"
            )

        filename_parts = whl_name.split("-")
        pkg_name = filename_parts[0].replace("_", "-")
        version = filename_parts[1]
        name_space = pkg_name.replace('-', '.')
        return pkg_name, version, name_space

    def _install_package(self, pkg_name):
        # Uninstall the package and reinstall it to parse so inspect can get members in package
        # We don't want to force reinstall to avoid reinstalling other dependent packages
        commands = [sys.executable, "-m", "pip", "uninstall", pkg_name, "--yes", "-q"]
        check_call(commands)
        commands = [sys.executable, "-m", "pip", "install", self.pkg_path , "-q"]
        check_call(commands)


class NodeIndex:
    """Maintains name to navigation ID"""
    def __init__(self):
        self.index = {}

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


def parse_setup_py(setup_path):
    """Parses setup.py and finds package name and version"""
    setup_filename = os.path.join(setup_path, "setup.py")
    mock_setup = textwrap.dedent(
        """\
    def setup(*args, **kwargs):
        __setup_calls__.append((args, kwargs))
    """
    )
    parsed_mock_setup = ast.parse(mock_setup, filename=setup_filename)
    with io.open(setup_filename, "r", encoding="utf-8-sig") as setup_file:
        parsed = ast.parse(setup_file.read())
        for index, node in enumerate(parsed.body[:]):
            if (
                not isinstance(node, ast.Expr)
                or not isinstance(node.value, ast.Call)
                or not hasattr(node.value.func, "id")
                or node.value.func.id != "setup"
            ):
                continue
            parsed.body[index:index] = parsed_mock_setup.body
            break

    fixed = ast.fix_missing_locations(parsed)
    codeobj = compile(fixed, setup_filename, "exec")
    local_vars = {}
    global_vars = {"__setup_calls__": []}
    current_dir = os.getcwd()
    working_dir = os.path.dirname(setup_filename)
    os.chdir(working_dir)
    exec(codeobj, global_vars, local_vars)
    os.chdir(current_dir)
    _, kwargs = global_vars["__setup_calls__"][0]
    package_name = kwargs["name"]
    name_space = package_name.replace('-', '.')
    if "packages" in kwargs.keys():
        packages = kwargs["packages"]
        if packages:
            name_space = packages[0]
            logging.info("Namespaces found for package {0}: {1}".format(package_name, packages))

    return package_name, kwargs["version"], name_space
