# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import os
import fnmatch
import re
import xml.etree.ElementTree as ET
import pathlib2

# python 3 transitioned StringIO to be part of `io` module.
# python 2 needs the old version however
try:
    from StringIO import StringIO
except ImportError:
    from io import StringIO

JS_PACKAGE_DISCOVERY_PATTERN = "*/package.json"
PYTHON_PACKAGE_DISCOVERY_PATTERN = "*/setup.py"
NET_PACKAGE_DISCOVERY_PATTERN = "*.csproj"
JAVA_PACKAGE_DISCOVERY_PATTERN = "*/pom.xml"
SWIFT_PACKAGE_DISCOVERY_PATTERN = "*/project.pbxproj"

# we want to walk the files as few times as possible. as such, for omitted_files, we provide a SET
# of patterns that we want to omit. This function simply checks
# directory mode being enabled will activate slightly different logic, and will do an additional match
# based on just the specific directory.
def check_match(file_path, normalized_target_patterns, directory_mode=False):
    return any(
        [
            fnmatch.fnmatch(file_path, normalized_target_pattern)
            or (
                directory_mode
                and os.path.dirname(file_path) == normalized_target_pattern
            )
            for normalized_target_pattern in normalized_target_patterns
        ]
    )


def get_java_package_roots(configuration):
    file_set = get_file_sets(
        configuration, JAVA_PACKAGE_DISCOVERY_PATTERN, is_java_pom_package_pom
    )

    if configuration.verbose_output:
        print(file_set)

    return file_set


def get_net_package(configuration):
    file_set = get_file_sets(
        configuration, NET_PACKAGE_DISCOVERY_PATTERN, is_net_csproj_package
    )

    if configuration.verbose_output:
        print(file_set)

    return file_set


def get_project_roots_from_pbxproj_paths(pbxproj_file_set):
    project_roots = []
    for pbxproj_file in pbxproj_file_set:
        pbxproj_file_path = pathlib2.Path(pbxproj_file)
        project_root_path = pbxproj_file_path.parents[1]
        project_roots.append(str(project_root_path))

    return project_roots


def get_swift_package_roots(configuration):
    project_files, omitted_project_files = get_file_sets(
        configuration, SWIFT_PACKAGE_DISCOVERY_PATTERN
    )

    project_roots = get_project_roots_from_pbxproj_paths(project_files)
    omitted_project_roots = get_project_roots_from_pbxproj_paths(omitted_project_files)

    file_set = project_roots, omitted_project_roots

    if configuration.verbose_output:
        print(file_set)

    return file_set


def get_python_package_roots(configuration):
    file_set = get_file_sets(configuration, PYTHON_PACKAGE_DISCOVERY_PATTERN)

    if configuration.verbose_output:
        print(file_set)

    return file_set


def get_js_package_roots(configuration):
    file_set = get_file_sets(configuration, JS_PACKAGE_DISCOVERY_PATTERN)

    if configuration.verbose_output:
        print(file_set)

    return file_set


# returns the two sets:
# the set of files where we expect a target_file to be present
# and the set of files that we expect a target_file to be present that have been explicitly omitted
def get_file_sets(configuration, target_pattern, lambda_check=None):
    expected_locations = walk_directory_for_pattern(
        configuration.target_directory, [target_pattern], configuration, lambda_check
    )
    omitted_files = get_omitted_files(configuration)

    return (
        list(set(expected_locations) - set(omitted_files)),
        list(set(omitted_files).intersection(expected_locations)),
    )


# gets the set of files in the target directory that have explicitly been omitted in the config settings
def get_omitted_files(configuration):
    repo_root = configuration.repo_root
    omitted_paths = []
    dirs = configuration.omitted_paths or []

    # single special case here. if wildcard match at the beginning, do not join, use the pattern as is
    adjusted_dirs = [
        pattern if pattern.startswith("*") else os.path.join(repo_root, pattern)
        for pattern in dirs
    ]
    omitted_paths.extend(
        walk_directory_for_pattern(
            repo_root, adjusted_dirs, configuration, None, True
        )
    )

    return omitted_paths


# convention. omit test projects
def is_net_csproj_package(file_path):
    test_proj_exclude = re.compile(
        ".*(\\\\|\/)(tests|samples)(\\\\|\/).*|.*test[s]?(\\|\/).csproj", re.IGNORECASE
    )

    if test_proj_exclude.match(file_path):
        return False

    return True


# Returns a list of files under a target directory. The files included will match any of the
# target_patterns AND the lambda_check function.
def walk_directory_for_pattern(
    target_directory,
    target_patterns,
    configuration,
    lambda_check=None,
    directory_mode=False,
):
    expected_locations = []
    target_directory = os.path.normpath(target_directory)
    normalized_target_patterns = [
        os.path.normpath(pattern) for pattern in target_patterns
    ]
    return_true = lambda x: True
    check_function = lambda_check or return_true

    # walk the folders, filter to the patterns established
    for folder, subfolders, files in os.walk(target_directory):
        for file in files:
            file_path = os.path.join(folder, file)

            if check_match(file_path, normalized_target_patterns, directory_mode):
                if configuration.verbose_output:
                    print(
                        "Pattern matched {}. Running Check Function.".format(file_path)
                    )
                if check_function(file_path):
                    expected_locations.append(file_path)
    return expected_locations


# given a file location or folder, check within or alongside for a target file
# case insensitive
def find_alongside_file(file_location, target):
    if not os.path.exists(file_location) or not target:
        return False

    rule = re.compile(fnmatch.translate(target), re.IGNORECASE)
    containing_folder = ""

    if os.path.isdir(file_location):
        # we're already looking at a file location. just check for presence of target in listdir
        containing_folder = file_location
    else:
        # os.path.listdir(os.path.dirname(file_location))
        containing_folder = os.path.dirname(file_location)

    for file in os.listdir(containing_folder):
        if file.lower() == target.lower():
            return os.path.normpath(os.path.join(containing_folder, file))
    return False


# find's the first file that matches a glob pattern under a target file's location
# case insensitive
def find_below_file(glob_pattern, file):
    if not os.path.exists(file) or not glob_pattern or os.path.isdir(file):
        return None
    rule = re.compile(fnmatch.translate(glob_pattern), re.IGNORECASE)

    target_directory = os.path.dirname(file)

    for folder, subfolders, files in os.walk(target_directory):
        for file in files:
            file_path = os.path.join(folder, file)
            if rule.match(file):
                return file_path


# searches upwards along from a specified file for a pattern
# glob pattern is the pattern we're matching against. often just a filename
# file is the file we're starting from
# path_exclusion_list the list of paths we should hard stop traversing up on if we haven't already exited
# early_exit_lambda_check a specific check that isn't only based on file. for .net we check to see of a .sln is present in the directory
def find_above_file(
    glob_pattern, file, path_exclusion_list, early_exit_lambda_check, root_directory
):
    if not os.path.exists(file) or not glob_pattern or os.path.isdir(file):
        return None

    if (
        path_exclusion_list is None or len(path_exclusion_list) == 0
    ) and early_exit_lambda_check is None:
        print(
            "Using find_above_file without at least one member set for package_indexing_traversal_stops in .docsettings OR setting an early_exit_lambda_check is disallowed. Exiting."
        )
        exit(1)

    complete_exclusion_list = path_exclusion_list + [root_directory]

    if early_exit_lambda_check is None:
        early_exit_lambda_check = lambda path: True

    target_rule = re.compile(fnmatch.translate(glob_pattern), re.IGNORECASE)

    file_dir = os.path.dirname(file)

    while not check_folder_against_exclusion_list(file_dir, complete_exclusion_list):
        for file in os.listdir(file_dir):
            if target_rule.match(file):
                return os.path.normpath(os.path.join(file_dir, file))
        # the early_exit_lambda check runs after we're done scanning the current directory for matches
        if early_exit_lambda_check(file_dir):
            return None
        file_dir = os.path.abspath(os.path.join(file_dir, "../"))
    return None


# True if folder matches anything in the exclusion list
# False if not
def check_folder_against_exclusion_list(folder, path_exclusion_list):
    if not os.path.isdir(folder):
        return True

    return os.path.normpath(folder) in path_exclusion_list


# given a pom.xml, crack it open and ensure that it is actually a package pom (versus a parent pom)
def is_java_pom_package_pom(file_path):
    root = parse_pom(file_path)
    artifactIdTag = root.find("parent/artifactId")

    if artifactIdTag is not None:
        return (
            artifactIdTag.text == "azure-client-sdk-parent"
            or artifactIdTag.text == "azure-data-sdk-parent"
        )
    return False


# namespaces in xml really mess with xmlTree: https://bugs.python.org/issue18304
# this function provides a workaround for both parsing an xml file as well as REMOVING said namespaces
def parse_pom(file_path):
    try:
        with open(file_path, "r", encoding="utf-8") as f:
            xml = f.read()
    except Exception as ex:
        print("Invalid XML in {}".format(file_path))
        raise ex

    it = ET.iterparse(StringIO(xml))
    for _, el in it:
        if "}" in el.tag:
            el.tag = el.tag.split("}", 1)[1]
    return it.root


# Parse csproj file to get version property
def parse_csproj(file_path):
    try:
        with open(file_path, "r", encoding="utf-8") as f:
            xml = f.read()
    except Exception as ex:
        print("Invalid XML in {}".format(file_path))
        raise ex

    it = ET.iterparse(StringIO(xml))
    for _, el in it:
        if el.tag == "Version":
            return el.text
            break
    return ""
