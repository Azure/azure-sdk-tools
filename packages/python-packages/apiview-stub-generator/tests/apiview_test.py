# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

import os
import sys
import tempfile
import shutil
from subprocess import check_call, run, PIPE
from pytest import fail, mark

from apistub import ApiView, TokenKind, StubGenerator, ReviewLines
from apistub.nodes import PylintParser

# Read in all init files from init_files folder and add the paths to INIT_PARAMS in the form of (file_name, file_path)
INIT_FILES_PATH = os.path.join(os.path.dirname(__file__), "init_files")


def _get_init_files(init_path=INIT_FILES_PATH):
    """
    Read in all init files from init_files and return them in the form of (file_name, file_path)
    """
    init_files = [os.path.join(init_path, f) for f in os.listdir(init_path)]
    init_params = []
    init_ids = []
    for init_file in init_files:
        file_name = os.path.basename(init_file)
        file_path = os.path.abspath(init_file)
        if ".extend_" in file_name:
            extends = True
        else:
            extends = False
        init_params.append((file_path, extends))
        init_ids.append(file_name)
    return init_params, init_ids


INIT_PARAMS, INIT_IDS = _get_init_files()


def _build_dist(src_dir, build_type, extension):
    check_call([sys.executable, "-m", "build", src_dir, f"--{build_type}"])
    dist_dir = os.path.join(src_dir, "dist")
    files = [f for f in os.listdir(dist_dir) if f.endswith(extension)]
    if not files:
        raise FileNotFoundError(f"No {build_type} file found in the dist directory")
    return os.path.join(dist_dir, files[0])


def _add_pyproject_package_to_temp(src_dir):
    temp_dir = tempfile.mkdtemp()
    dest_dir = os.path.join(temp_dir, os.path.basename(src_dir) + "-copied")
    shutil.copytree(src_dir, dest_dir)

    # Remove setup.py and add pyproject.toml
    setup_py_path = os.path.join(dest_dir, "setup.py")
    assert os.path.exists(setup_py_path)

    # Copy _pyproject.toml from tests folder to the package copy folder
    tests_dir_pyproject = os.path.join(os.path.dirname(__file__), "_pyproject.toml")
    pyproject_path = os.path.join(dest_dir, "pyproject.toml")
    shutil.copy(tests_dir_pyproject, pyproject_path)

    assert os.path.exists(pyproject_path)
    os.remove(setup_py_path)
    assert not os.path.exists(setup_py_path)

    # Move the new mapping file to the old mapping file name
    mapping_file_path = os.path.join(dest_dir, "apiview-properties.json")
    old_mapping_file_path = os.path.join(dest_dir, "apiview_mapping_python.json")
    shutil.move(mapping_file_path, old_mapping_file_path)
    assert os.path.exists(old_mapping_file_path)
    assert not os.path.exists(mapping_file_path)

    return dest_dir


PKG_PATH = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "apistubgentest"))
SDIST_PATH = _build_dist(PKG_PATH, "sdist", ".tar.gz")
WHL_PATH = _build_dist(PKG_PATH, "wheel", ".whl")

PYPROJECT_PKG_PATH = _add_pyproject_package_to_temp(PKG_PATH)
PYPROJECT_SDIST_PATH = _build_dist(PYPROJECT_PKG_PATH, "sdist", ".tar.gz")
PYPROJECT_WHL_PATH = _build_dist(PYPROJECT_PKG_PATH, "wheel", ".whl")

PYPROJECT_PATHS = [PYPROJECT_PKG_PATH, PYPROJECT_WHL_PATH, PYPROJECT_SDIST_PATH]
PYPROJECT_IDS = ["pyproject-source", "pyproject-whl", "pyproject-sdist"]

ALL_PATHS = [PKG_PATH, WHL_PATH, SDIST_PATH, PYPROJECT_PKG_PATH, PYPROJECT_WHL_PATH, PYPROJECT_SDIST_PATH]
ALL_PATH_IDS = ["setup-source", "setup-whl", "setup-sdist", "pyproject-source", "pyproject-whl", "pyproject-sdist"]

MAPPING_FILE_NAME = "apiview-properties.json"
OLD_MAPPING_FILE_NAME = "apiview_mapping_python.json"
MAPPING_PATHS = [(PKG_PATH, MAPPING_FILE_NAME), (PYPROJECT_PKG_PATH, OLD_MAPPING_FILE_NAME)]
MAPPING_IDS = [mapping_file for _, mapping_file in MAPPING_PATHS]


class TestApiView:
    def _count_newlines(self, apiview: ApiView):
        newline_count = 0
        for line in apiview.review_lines[::-1]:
            if len(line.tokens) == 0:
                newline_count += 1
            else:
                break
        return newline_count

    # Validates that there are no repeat defintion IDs and that each line has only one definition ID.
    def _validate_line_ids(self, apiview: ApiView):
        line_ids = set()

        def collect_line_ids(review_lines, index=0):
            for line in review_lines:
                # Ensure that there are no repeated definition IDs.
                if line.line_id and line.line_id in line_ids:
                    fail(f"Duplicate definition ID {line.line_id}.")
                    line_ids.add(line.line_id)
                # Recursively collect definition IDs from child lines
                if line.children:
                    collect_line_ids(line.children, index)

        collect_line_ids(apiview.review_lines)

    def _add_duplicate_line_id(self, apiview: ApiView):
        # Copy the very last review_line and append it to the end
        if apiview.review_lines:
            last_line = apiview.review_lines[-1]
            # remove all lines but duplicates for faster test run
            apiview.review_lines = ReviewLines()
            apiview.review_lines.append(last_line)
            apiview.review_lines.append(last_line)
        return apiview

    def _dependency_installed(self, dep):
        result = run([sys.executable, "-m", "pip", "show", dep], stdout=PIPE, stderr=PIPE, text=True)
        # return code 1 means the package is not installed
        return result.returncode == 0

    def _uninstall_dep(self, dep):
        if self._dependency_installed(dep):
            check_call([sys.executable, "-m", "pip", "uninstall", "-y", dep])
            try:
                for module in list(sys.modules):
                    if module.startswith(dep):
                        del sys.modules[module]
            except KeyError:
                pass
        assert not self._dependency_installed(dep)

    @mark.parametrize("pkg_path", ALL_PATHS, ids=ALL_PATH_IDS)
    def test_optional_dependencies(self, pkg_path):
        # uninstall optional dependencies if installed
        for dep in ["httpx", "pandas"]:
            self._uninstall_dep(dep)
        # uninstall apistubgentest if installed, so new install will be from pkg_path
        self._uninstall_dep("apistubgentest")
        # if pkg is src, rm *.egg-info from path to check that pkg metadata parsing works
        if os.path.isdir(pkg_path):
            for f in os.listdir(pkg_path):
                if f == "apistubgentest.egg-info":
                    shutil.rmtree(os.path.join(pkg_path, f))
                    break
        temp_path = tempfile.gettempdir()
        stub_gen = StubGenerator(pkg_path=pkg_path, temp_path=temp_path)
        apiview = stub_gen.generate_tokens()
        for dep in ["httpx", "pandas"]:
            assert self._dependency_installed(dep)
        # skip conditional optional dependencies
        assert not self._dependency_installed("qsharp")
        # assert package name is correct
        assert apiview.package_name == "apistubgentest"

    @mark.parametrize("pkg_path", PYPROJECT_PATHS, ids=PYPROJECT_IDS)
    def test_pyproject_toml_line_ids(self, pkg_path):
        # uninstall apistubgentest if installed, so new install will be from pkg_path
        self._uninstall_dep("apistubgentest")
        temp_path = tempfile.gettempdir()
        stub_gen = StubGenerator(pkg_path=pkg_path, temp_path=temp_path, verbose=True)
        apiview = stub_gen.generate_tokens()
        self._validate_line_ids(apiview)

    def test_multiple_newline_only_add_one(self):
        apiview = ApiView()
        review_line = apiview.review_lines.create_review_line()
        review_line.add_text("Something")
        apiview.review_lines.append(review_line)
        apiview.review_lines.set_blank_lines()
        # subsequent calls result in no change
        apiview.review_lines.set_blank_lines()
        apiview.review_lines.set_blank_lines()
        assert self._count_newlines(apiview) == 1

    def test_set_blank_lines(self):
        apiview = ApiView()
        apiview.review_lines.set_blank_lines(3)
        assert self._count_newlines(apiview) == 3

        review_line = apiview.review_lines.create_review_line()
        review_line.add_text("Something")
        apiview.review_lines.set_blank_lines(1)
        apiview.review_lines.set_blank_lines(5)
        # only the last invocation matters
        apiview.review_lines.set_blank_lines(2)
        assert self._count_newlines(apiview) == 2

    def test_api_view_diagnostic_warnings(self):
        temp_path = tempfile.gettempdir()
        stub_gen = StubGenerator(pkg_path=PKG_PATH, temp_path=temp_path)
        apiview = stub_gen.generate_tokens()
        # ensure we have only the expected diagnostics when testing apistubgentest
        unclaimed = PylintParser.get_unclaimed()
        assert len(apiview.diagnostics) == 93
        # The "needs copyright header" error corresponds to a file, which isn't directly
        # represented in APIView
        assert len(unclaimed) == 1

    def test_api_view_diagnostic_no_duplicate(self):
        """Verify no duplicate diagnostics exist.

        Validates that diagnostics from classes, methods with @overload, enums, and legacy typing
        are not duplicated (regression test for issue where class-level errors appeared for each method).
        """
        temp_path = tempfile.gettempdir()
        stub_gen = StubGenerator(pkg_path=PKG_PATH, temp_path=temp_path)
        apiview = stub_gen.generate_tokens()

        # Check for duplicates
        all_keys = [(d['Text'], d['TargetId']) for d in apiview.diagnostics]
        duplicates = [k for k in set(all_keys) if all_keys.count(k) > 1]
        assert len(duplicates) == 0, f"Found duplicate diagnostics: {duplicates}"

        # Verify PylintCheckerViolationsClient has diagnostics at class, constructor, and method levels
        violations_diags = [d for d in apiview.diagnostics if 'PylintCheckerViolationsClient' in d['TargetId']]
        class_level = [d for d in violations_diags if d['TargetId'] == 'apistubgentest.PylintCheckerViolationsClient']
        constructor_level = [d for d in violations_diags if d['TargetId'] == 'apistubgentest.PylintCheckerViolationsClient.__init__']
        method_level = [d for d in violations_diags if 'with_too_many_args' in d['TargetId'] or 'list_secrets' in d['TargetId'] or 'set_secret' in d['TargetId'] or 'get_secret' in d['TargetId']]

        assert len(violations_diags) == 16, f"Should have 16 total diagnostics, got {len(violations_diags)}"
        assert len(class_level) == 2, f"Should have 2 class-level diagnostics, got {len(class_level)}"
        assert len(constructor_level) == 2, f"Should have 2 constructor-level diagnostics, got {len(constructor_level)}"
        assert len(method_level) == 12, f"Should have 12 method-level diagnostics (including overloads), got {len(method_level)}"

        # Verify that overload methods with @distributed_trace have diagnostics without duplicates
        list_secrets_overload1_diags = [d for d in violations_diags if 'list_secrets_1' in d['TargetId']]
        set_secret_overload2_diags = [d for d in violations_diags if 'set_secret_2' in d['TargetId']]

        # Each overloaded method should have diagnostics for overloads + implementation
        assert len(list_secrets_overload1_diags) == 2 , f"list_secrets overload 1 should have diagnostics for overloads and implementation, got {len(list_secrets_overload1_diags)}"
        assert len(set_secret_overload2_diags) == 3, f"set_secret overload 2 should have diagnostics for overloads and implementation, got {len(set_secret_overload2_diags)}"

        # Verify enum value and property diagnostics exist
        enum_value_diags = [d for d in apiview.diagnostics if d['TargetId'] == 'apistubgentest.PylintViolationEnum.password' or d['TargetId'] == 'apistubgentest.PylintViolationEnum.CERTIFICATE']
        property_diags = [d for d in apiview.diagnostics if 'handwritten_property' in d['TargetId']]

        assert len(enum_value_diags) == 2, f"Should have 2 enum value diagnostics for PylintViolationEnum.password and PylintViolationEnum.CERTIFICATE, got {len(enum_value_diags)}"
        assert len(property_diags) == 1, f"Should have 1 property diagnostic, got {len(property_diags)}"

    def test_add_type(self):
        apiview = ApiView()
        review_line = apiview.review_lines.create_review_line()
        review_line.add_type(type_name="a.b.c.1.2.3.MyType", apiview=apiview)
        apiview.review_lines.append(review_line)
        tokens = review_line.tokens
        assert len(tokens) == 1
        assert tokens[0].kind == TokenKind.TYPE_NAME

    def test_line_ids(self):
        temp_path = tempfile.gettempdir()
        stub_gen = StubGenerator(pkg_path=PKG_PATH, temp_path=temp_path)
        apiview = stub_gen.generate_tokens()
        self._validate_line_ids(apiview)

        # If there ARE duplicate line IDs, check that StubGenerator will raise an error
        apiview = self._add_duplicate_line_id(apiview)
        try:
            stub_gen.check_unique_line_ids(apiview)
            # Ensure that unique IDs fails
            fail(f"No duplicate definition IDs found.")
        except ValueError:
            pass

    @mark.parametrize("pkg_path, mapping_file", MAPPING_PATHS, ids=MAPPING_IDS)
    def test_mapping_file(self, pkg_path, mapping_file):
        # Check that mapping file exists
        mapping_file_path = os.path.join(pkg_path, mapping_file)
        assert os.path.exists(mapping_file_path)

        temp_path = tempfile.gettempdir()
        stub_gen = StubGenerator(pkg_path=pkg_path, temp_path=temp_path, mapping_path=mapping_file_path)
        # test passing in the new mapping file path which doesn't exist, so that that the default old one will be used
        if mapping_file == OLD_MAPPING_FILE_NAME:
            mapping_file_path = os.path.join(pkg_path, MAPPING_FILE_NAME)
            stub_gen = StubGenerator(pkg_path=pkg_path, temp_path=temp_path, mapping_path=mapping_file_path)
        apiview = stub_gen.generate_tokens()
        self._validate_line_ids(apiview)
        cross_language_lines = []

        def get_cross_language_id(review_lines):
            for line in review_lines:
                if line.cross_language_id:
                    cross_language_lines.append(line)
                if line.children:
                    get_cross_language_id(line.children)

        get_cross_language_id(apiview.review_lines)
        assert cross_language_lines[0].cross_language_id == "Formal_Model_Id"
        assert cross_language_lines[1].cross_language_id == "Docstring_DocstringWithFormalDefault"
        assert len(cross_language_lines) == 2
        assert apiview.cross_language_metadata.cross_language_package_id == "ApiStubGenTest"

    def test_source_url(self):
        temp_path = tempfile.gettempdir()
        stub_gen = StubGenerator(pkg_path=PKG_PATH, temp_path=temp_path, source_url="https://www.bing.com/")
        apiview = stub_gen.generate_tokens()
        # Check that TokenKind is EXTERNAL_URL
        assert apiview.review_lines[2]["Tokens"][1]["Kind"] == 8

    @mark.parametrize("file_path, extends", INIT_PARAMS, ids=INIT_IDS)
    def test_set_namespace(self, file_path, extends):
        stub_gen = StubGenerator(pkg_path=PKG_PATH)
        stub_gen._set_root_namespace(file_path, "namespace")
        # If the file is an extend_ file, the namespace should not be set
        assert (stub_gen.namespace == "") if extends else (stub_gen.namespace == "namespace")
