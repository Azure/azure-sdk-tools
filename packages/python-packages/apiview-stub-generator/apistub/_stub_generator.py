#!/usr/bin/env python

# --------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.
# --------------------------------------------------------------------------------------------

import importlib.metadata
import sys
import os
import argparse
from pkginfo import get_metadata
from typing import Dict

import importlib
import logging
import shutil
import tempfile
from subprocess import check_call
import zipfile
import tarfile
import re
try:
    import tomllib
except ModuleNotFoundError:
    import tomli as tomllib

from apistub._metadata_map import MetadataMap

from apistub._generated.treestyle.parser.models import ApiView
from apistub._generated.treestyle.parser._model_base import (
    SdkJSONEncoder as APIViewEncoder,
)

INIT_PY_FILE = "__init__.py"
INIT_EXTENSION_SUBSTRING = ".extend_path(__path__, __name__)"

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
                "--pkg-path",
                required=True,
                help=("Path to the package source root, WHL, ZIP, or TAR file."),
            )
            parser.add_argument(
                "--temp-path",
                help=(
                    "Extract the package to the specified temporary path. Defaults to a random temp dir."
                ),
                default=tempfile.gettempdir(),
            )
            parser.add_argument(
                "--out-path",
                default=os.getcwd(),
                help=(
                    "Path at which to write the generated JSON file. Defaults to CWD."
                ),
            )
            parser.add_argument(
                "--mapping-path",
                default=None,
                help=(
                    "Path to an 'apiview-properties.json' file that supplies cross-language definition IDs."
                ),
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
                help=(
                    "URL to the pull request URL that contains the source used to generate this APIView."
                ),
            )
            parser.add_argument(
                "--skip-pylint",
                help=("Skips running pylint on the package to obtain diagnostics."),
                default=False,
                action="store_true",
            )
            parser.add_argument(
                "--md",
                help=("Generate markdown output in addition to JSON."),
                default=False,
                action="store_true",
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
        md = self._parse_arg("md")

        if not os.path.exists(pkg_path):
            logging.error("Package path [{}] is invalid".format(pkg_path))
            exit(1)
        elif not os.path.exists(temp_path):
            logging.error("Temp path [{0}] is invalid".format(temp_path))
            exit(1)

        if os.path.isdir(pkg_path):
            pkg_path = os.path.abspath(pkg_path)
        self.pkg_path = pkg_path
        self.temp_path = temp_path
        self.out_path = out_path
        self.source_url = source_url
        self.mapping_path = mapping_path
        self.filter_namespace = filter_namespace or ""
        self.namespace = ""
        self.md = md if md is not None else False
        if verbose:
            logging.getLogger().setLevel(logging.DEBUG)

        # Extract package to temp directory if it is wheel or sdist
        if self.pkg_path.endswith((".whl", ".zip", ".tar.gz")):
            self.wheel_path = self._extract_wheel()
        else:
            self.wheel_path = None

        if not skip_pylint:
            PylintParser.parse(self.wheel_path or self.pkg_path)

    def _parse_arg(self, name):
        # Check if the argument was passed as a kwarg first
        if name in self._kwargs:
            return self._kwargs[name]
        # Otherwise try to get it from parsed command-line arguments
        try:
            return getattr(self._args, name, None)
        except AttributeError:
            return None

    def install_extra_dependencies(self):
        for extra in self.extras_require:
            if ":" in extra:
                logging.info(f"Skipping conditional extra dependency: {extra}")
                continue
            logging.info(f"Installing extra dependency: {extra}")
            try:
                check_call(
                    [
                        sys.executable,
                        "-m",
                        "pip",
                        "install",
                        f"{self.pkg_path}[{extra}]",
                        "-q",
                    ]
                )
            except:
                # If we can't install the extra dependency, skip and continue
                logging.info(f"Failed to install extra dependency: {extra}")
                pass

    def _get_pkg_metadata(self):
        # pkginfo does not get package metadata in 3.10 when running against package root path
        if not self.wheel_path:
            pkg_root_path = self.pkg_path
            pkg_name = os.path.split(self.pkg_path)[-1]
            try:
                version = importlib.metadata.version(pkg_name)
            except importlib.metadata.PackageNotFoundError:
                # If the package name from directory doesn't match actual package name,
                # try to get it from metadata files
                pkg_name = self._get_package_name_from_metadata_files(self.pkg_path)
                if not pkg_name:
                    # If we still can't find it, re-raise the original error
                    raise
                version = importlib.metadata.version(pkg_name)
            dist = importlib.metadata.distribution(pkg_name)
            self.extras_require = dist.metadata.get_all('Provides-Extra') or []
            return pkg_root_path, pkg_name, version
        pkg_root_path = self.wheel_path
        metadata = get_metadata(self.pkg_path)
        pkg_name = metadata.name
        version = metadata.version
        self.extras_require = metadata.provides_extras
        return pkg_root_path, pkg_name, version

    def generate_tokens(self):
        # TODO: We should install to a virtualenv
        logging.debug("Installing package from {}".format(self.pkg_path))
        self._install_package()
        pkg_root_path, pkg_name, version = self._get_pkg_metadata()
        logging.info(
            "package name: {0}, version:{1}".format(
                pkg_name, version
            )
        )
        if self.filter_namespace:
            logging.info(
                "Namespace filter is passed. Filtering modules within namespace :{}".format(
                    self.filter_namespace
                )
            )
            self.namespace = self.filter_namespace

        logging.debug("Generating tokens")
        try:
            apiview = self._generate_tokens(
                pkg_root_path, pkg_name, version, source_url=self.source_url
            )
        except ImportError as import_exc:
            logging.info(f"{import_exc}\nInstalling extra dependencies.")
            self.install_extra_dependencies()
            # Retry generating tokens
            apiview = self._generate_tokens(
                pkg_root_path, pkg_name, version, source_url=self.source_url
            )

        self.check_unique_line_ids(apiview)

        if apiview.diagnostics:
            logging.info(
                "*************** Completed parsing package with errors ***************"
            )
        else:
            logging.info(
                "*************** Completed parsing package and generating tokens ***************"
            )
        return apiview

    def check_unique_line_ids(self, apiview: ApiView):
        def check_line_ids(review_lines):
            """Recursively check LineIds in ReviewLines and their Children."""
            for line in review_lines:
                if "LineId" in line and line["LineId"]:
                    line_id = line["LineId"]
                    if line_id in line_ids:
                        duplicate_ids.append(line_id)
                    else:
                        line_ids.add(line_id)

                # Check children recursively
                if "Children" in line and line["Children"]:
                    check_line_ids(line["Children"])

        line_ids: set[str] = set()
        duplicate_ids: list[str] = []
        check_line_ids(apiview["ReviewLines"])

        # Raise error with all duplicated ids
        if duplicate_ids:
            raise ValueError(f"Duplicate LineIds found: {duplicate_ids}")

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
            # Ignore build, which is created when installing a package from source.
            # Ignore tests, which may have an __init__.py but is not part of the package.
            dirs_to_skip = [x for x in subdirs if x.startswith(("_", ".", "test", "build"))]
            for d in dirs_to_skip:
                logging.debug("Dirs to skip: {}".format(dirs_to_skip))
                subdirs.remove(d)
            if INIT_PY_FILE in files:
                module_name = os.path.relpath(root, pkg_root_path).replace(
                    os.path.sep, "."
                )
                # If namespace has not been set yet, try to find the first __init__.py that's not purely for extension.
                if not self.namespace:
                    self._set_root_namespace(
                        os.path.join(root, INIT_PY_FILE), module_name
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

    def _set_root_namespace(self, init_file_path, module_name):
        with open(init_file_path, "r") as f:
            in_docstring = False
            content = []
            for line in f:
                stripped_line = line.strip()
                # If in multi-line docstring, skip following lines until end of docstring.
                # If single-line docstring, skip the docstring line.
                if stripped_line.startswith(('"""', "'''")) and not stripped_line.endswith(('"""', "'''")):
                    in_docstring = not in_docstring
                # If comment, skip line. Otherwise, add to content.
                if not in_docstring and not stripped_line.startswith("#"):
                    content.append(line)
            if len(content) > 1 or (
                len(content) == 1 and not INIT_EXTENSION_SUBSTRING in content[0]
            ):
                self.namespace = module_name

    def _generate_tokens(
        self, pkg_root_path, package_name, package_version, *, source_url
    ):
        """This method returns a dictionary of namespace and all public classes in each namespace"""
        # Import ModuleNode.
        # Importing it globally can cause circular dependency since it needs NodeIndex that is defined in this file
        from apistub.nodes._module_node import ModuleNode
        from apistub.nodes import PylintParser

        self.module_dict = {}
        mapping = MetadataMap(pkg_root_path, mapping_path=self.mapping_path)
        modules = self._find_modules(pkg_root_path)
        logging.debug("Modules to generate tokens: {}".format(modules))

        apiview = ApiView(
            pkg_name=package_name,
            metadata_map=mapping,
            namespace=self.namespace,
            source_url=source_url,
            pkg_version=package_version,
        )
        apiview.generate_tokens()

        # load all modules and parse them recursively
        for m in modules:
            if not m.startswith(self.namespace):
                logging.debug(
                    "Skipping module {0}. Module should start with {1}".format(
                        m, self.namespace
                    )
                )
                continue

            logging.debug("Importing module {}".format(m))
            module_obj = importlib.import_module(m)
            self.module_dict[m] = ModuleNode(
                m, module_obj, self.namespace, apiview=apiview
            )

        ## Generate any global diagnostics
        global_errors = PylintParser.get_items("GLOBAL")
        for g in global_errors or []:
            g.generate_tokens(apiview, "GLOBAL")

        # Generate tokens
        modules = self.module_dict.keys()
        for m in modules:
            self.module_dict[m].generate_diagnostics()
            # Generate and add token to APIView
            logging.debug("Generating tokens for module {}".format(m))
            self.module_dict[m].generate_tokens(apiview.review_lines)
        return apiview

    def _extract_wheel(self):
        """Extract the wheel into out dir and return root path to azure root directory in package"""
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
        if self.pkg_path.endswith(".tar.gz"):
            with tarfile.open(self.pkg_path) as tar_ref:
                tar_ref.extractall(temp_pkg_dir)
                self._remove_extra_internal_folder(temp_pkg_dir)
        else:
            with zipfile.ZipFile(self.pkg_path) as zip_ref:
                zip_ref.extractall(temp_pkg_dir)
        logging.debug("Extracted package files into temp path")

        return temp_pkg_dir

    def _remove_extra_internal_folder(self, temp_pkg_dir):
        contents = os.listdir(temp_pkg_dir)
        if len(contents) == 1 and os.path.isdir(
            os.path.join(temp_pkg_dir, contents[0])
        ):
            internal_folder = os.path.join(temp_pkg_dir, contents[0])
            for item in os.listdir(internal_folder):
                shutil.move(os.path.join(internal_folder, item), temp_pkg_dir)
            os.rmdir(internal_folder)

    def _get_package_name_from_metadata_files(self, path):
        """Extract the package name from metadata files in the given directory.

        This function first attempts to extract the package name from a `pyproject.toml` file.
        If that fails, it falls back to parsing a `setup.py` file for a `PACKAGE_NAME` variable.
        """
        pkg_name = None

        # try pyproject.toml
        pyproject_path = os.path.join(path, 'pyproject.toml')
        if os.path.exists(pyproject_path):
            try:
                with open(pyproject_path, 'rb') as f:
                    data = tomllib.load(f)
                if 'project' in data and 'name' in data['project']:
                    pkg_name = data['project']['name']
            except Exception:
                pass

        # try setup.py for package_name = "name" pattern
        if not pkg_name:
            setup_py_path = os.path.join(path, 'setup.py')
            if os.path.exists(setup_py_path):
                try:
                    with open(setup_py_path, 'r') as f:
                        content = f.read()
                    # Look for PACKAGE_NAME = "package-name"
                    match = re.search(r'PACKAGE_NAME\s*=\s*["\']([^"\']+)["\']', content)
                    if match:
                        pkg_name = match.group(1)
                except Exception as exc:
                    logging.warning(f"Failed to read setup.py or parse PACKAGE_NAME: {exc}")

        return pkg_name

    def _install_package(self):
        commands = [sys.executable, "-m", "pip", "install", self.pkg_path, "-q"]
        check_call(commands, timeout=120)
