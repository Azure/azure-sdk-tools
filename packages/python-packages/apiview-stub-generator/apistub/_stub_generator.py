#!/usr/bin/env python

# --------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.
# --------------------------------------------------------------------------------------------

import glob
import sys
import os
import argparse

import io
import importlib
import logging
import shutil
import ast
import textwrap
import tempfile
from subprocess import check_call
import zipfile


from apistub._apiview import ApiView, APIViewEncoder, Navigation, Kind, NavigationTag
from apistub._metadata_map import MetadataMap

INIT_PY_FILE = "__init__.py"
TOP_LEVEL_WHEEL_FILE = "top_level.txt"

logging.getLogger().setLevel(logging.ERROR)


class StubGenerator:
    def __init__(self, **kwargs):
        from .nodes import PylintParser
        self._kwargs = kwargs
        if not kwargs:
            parser = argparse.ArgumentParser(
                description="Parses a Python package and generates a JSON token file for consumption by the APIView tool."
            )
            parser.add_argument(
                "--pkg-path", required=True, help=("Path to the package source root, WHL or ZIP file."),
            )
            parser.add_argument(
                "--temp-path", 
                help=("Extract the package to the specified temporary path. Defaults to a random temp dir."),
                default=tempfile.gettempdir(),
            )
            parser.add_argument(
                "--out-path",
                default=os.getcwd(),
                help=("Path at which to write the generated JSON file. Defaults to CWD."),
            )
            parser.add_argument(
                "--mapping-path",
                default=None,
                help=("Path to an 'apiview_mapping.json' file that supplies cross-langauge definition IDs.")
            )
            parser.add_argument(
                "--verbose",
                help=("Enable verbose logging."),
                default=False,
                action="store_true",
            )
            parser.add_argument(
                "--filter-namespace",
                help=("Generate APIView only for a specific namespace."),
            )
            parser.add_argument(
                "--source-url",
                help=("URL to the pull request URL that contains the source used to generate this APIView.")
            )
            parser.add_argument(
                "--skip-pylint",
                help=("Skips running pylint on the package to obtain diagnostics."),
                default=False,
                action="store_true"
            )
            self._args = parser.parse_args()

        pkg_path = self._parse_arg("pkg_path")
        temp_path = self._parse_arg("temp_path") or tempfile.gettempdir()
        out_path = self._parse_arg("out_path")
        mapping_path = self._parse_arg("mapping_path")
        verbose = self._parse_arg("verbose")
        filter_namespace = self._parse_arg("filter_namespace")
        source_url = self._parse_arg("source_url")
        skip_pylint = self._parse_arg("skip_pylint")

        if not os.path.exists(pkg_path):
            logging.error("Package path [{}] is invalid".format(pkg_path))
            exit(1)
        elif not os.path.exists(temp_path):
            logging.error("Temp path [{0}] is invalid".format(temp_path))
            exit(1)

        self.pkg_path = pkg_path
        self.temp_path = temp_path
        self.out_path = out_path
        self.source_url = source_url
        self.mapping_path = mapping_path
        self.filter_namespace = filter_namespace or ''
        if verbose:
            logging.getLogger().setLevel(logging.DEBUG)

        # Extract package to temp directory if it is wheel or sdist
        if self.pkg_path.endswith(".whl") or self.pkg_path.endswith(".zip"):
            self.wheel_path = self._extract_wheel()
        else:
            self.wheel_path = None

        if not skip_pylint:
            PylintParser.parse(self.wheel_path or self.pkg_path)

    def _parse_arg(self, name):
        value = self._kwargs.get(name, None)
        if not value:
            try:
                value = getattr(self._args, name, None)
            except AttributeError:
                value = None
        return value

    def generate_tokens(self):
        # Extract package to temp directory if it is wheel or sdist
        if self.pkg_path.endswith(".whl") or self.pkg_path.endswith(".zip"):
            pkg_root_path = self.wheel_path
            pkg_name, version = self._parse_pkg_name()
            namespace = self.get_module_root_name(pkg_root_path)
        else:
            # package root is passed as arg to parse
            pkg_root_path = self.pkg_path
            pkg_name, version, namespace = parse_setup_py(pkg_root_path)

        logging.debug("package name: {0}, version:{1}, namespace:{2}".format(pkg_name, version, namespace))

        # TODO: We should install to a virtualenv
        logging.debug("Installing package from {}".format(self.pkg_path))
        self._install_package(pkg_name)
        
        if self.filter_namespace:
            logging.info("Namespace filter is passed. Filtering modules within namespace :{}".format(self.filter_namespace))
            namespace = self.filter_namespace

        logging.debug("Generating tokens")
        apiview = self._generate_tokens(pkg_root_path, pkg_name, namespace, source_url=self.source_url)
        if apiview.diagnostics:
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
        return sorted(modules)


    def _generate_tokens(self, pkg_root_path, package_name, namespace, *, source_url):
        """This method returns a dictionary of namespace and all public classes in each namespace
        """
        # Import ModuleNode.
        # Importing it globally can cause circular dependency since it needs NodeIndex that is defined in this file
        from apistub.nodes._module_node import ModuleNode
        from apistub.nodes import PylintParser

        self.module_dict = {}
        mapping = MetadataMap(pkg_root_path, mapping_path=self.mapping_path)
        apiview = ApiView(
            pkg_name=package_name,
            namespace=namespace,
            metadata_map=mapping,
            source_url=source_url
        )
        modules = self._find_modules(pkg_root_path)
        logging.debug("Modules to generate tokens: {}".format(modules))

        # load all modules and parse them recursively
        for m in modules:
            if not m.startswith(namespace):
                logging.debug("Skipping module {0}. Module should start with {1}".format(m, namespace))
                continue

            logging.debug("Importing module {}".format(m))
            module_obj = importlib.import_module(m)
            self.module_dict[m] = ModuleNode(m, module_obj, apiview.node_index, namespace)

        # Create navigation info to navigate within APIreview tool
        navigation = Navigation(package_name, None)
        navigation.tags = NavigationTag(Kind.type_package)
        apiview.add_navigation(navigation)

        # Generate any global diagnostics
        global_errors = PylintParser.get_items("GLOBAL")
        for g in global_errors or []:
            g.generate_tokens(apiview, "GLOBAL")

        # Generate tokens
        modules = self.module_dict.keys()
        for m in modules:
            self.module_dict[m].generate_diagnostics()
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


    def get_module_root_name(self, wheel_extract_path):
        # APiStubgen finds namespace from setup.py when running against code repo
        # But we don't have setup.py to parse when wheel is uploaded into APIView tool
        # Parse top_level.txt file in dist-info to find root module name
        files = glob.glob(os.path.join(wheel_extract_path, "*", TOP_LEVEL_WHEEL_FILE))
        if not files:
            logging.warning("File {0} is not found in {1} to identify root module name. All mdoules in package will be parsed".format(TOP_LEVEL_WHEEL_FILE, wheel_extract_path))
            return ""
        with io.open(files[0], "r") as top_lvl_file:
            root_module_name = top_lvl_file.readline().strip()
            logging.info("Root module found in {0}: '{1}'".format(TOP_LEVEL_WHEEL_FILE, root_module_name))
            return root_module_name


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
        return pkg_name, version

    def _install_package(self, pkg_name):
        commands = [sys.executable, "-m", "pip", "install", self.pkg_path , "-q"]
        check_call(commands, timeout = 60)


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
