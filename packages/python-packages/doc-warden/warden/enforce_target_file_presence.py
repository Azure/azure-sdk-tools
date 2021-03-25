# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from __future__ import print_function

import os
import glob
import fnmatch
import zipfile
from pathlib2 import Path
from .warden_common import get_java_package_roots, get_net_package, get_python_package_roots, get_swift_package_roots, get_js_package_roots, find_alongside_file

# python 3 transitioned StringIO to be part of `io` module. 
# python 2 needs the old version however
try:
    from StringIO import StringIO
except ImportError:
    from io import StringIO

# default option for handling an uncrecognized language
def unrecognized_option(configuration):
    print('Argument {} provided is not a supported option'.format(configuration.scan_language))
    exit(1)

# parent caller for language types
def find_missing_target_files(configuration):
    language_selector = {
        'python': check_python_target_files,
        'js': check_js_target_files,
        'java': check_java_target_files,
        'net': check_net_target_files,
        'swift': check_swift_target_files
    }
    missing_target_file_paths = []
    ignored_missing_target_file_paths = []
    known_issue_paths = configuration.get_known_presence_issues()
    target_file_results = language_selector.get(configuration.scan_language.lower(), unrecognized_option)(configuration)

    for target_file_path in target_file_results:
        if target_file_path in known_issue_paths:
            ignored_missing_target_file_paths.append(target_file_path)
        else:
            missing_target_file_paths.append(target_file_path)

    return missing_target_file_paths, ignored_missing_target_file_paths

# check the root of the target_directory for a default README 
def check_repo_root(configuration):
    if configuration.root_check_enabled:
        # check root for readme.md
        present_files = [f for f in os.listdir(configuration.target_directory) if os.path.isfile(os.path.join(configuration.target_directory, f))]
        return  any(x in [f.lower() for f in present_files] for x in configuration.target_files)
    return true

def check_swift_target_files(configuration):
    expected_target_files, omitted_target_files = get_swift_package_roots(configuration)
    missing_expected_target_file_locations = []

    for expected_location in expected_target_files:
        result = False
        for target_file in configuration.target_files:
            result = result or find_alongside_file(expected_location, target_file)
        if not result:
            missing_expected_target_file_locations.append(os.path.dirname(expected_location))
    return missing_expected_target_file_locations

# return all missing target_files for a PYTHON repostiroy
def check_python_target_files(configuration):
    expected_target_files, omitted_target_files = get_python_package_roots(configuration)
    missing_expected_target_file_locations = []

    for expected_location in expected_target_files:
        result = False
        for target_file in configuration.target_files:
            result = result or find_alongside_file(expected_location, target_file)
        if not result:
            missing_expected_target_file_locations.append(os.path.dirname(expected_location))
    return missing_expected_target_file_locations

# return all missing target_files for a JAVASCRIPT repository
def check_js_target_files(configuration):
    expected_target_files, omitted_target_files = get_js_package_roots(configuration)
    missing_expected_target_file_locations = []

    for expected_location in expected_target_files:
        result = False
        for target_file in configuration.target_files:
            result = result or find_alongside_file(expected_location, target_file)
        if not result:
            missing_expected_target_file_locations.append(os.path.dirname(expected_location))
    return missing_expected_target_file_locations

# return all missing target_files for a .NET repostory
def check_net_target_files(configuration):
    expected_target_files, omitted_target_files = get_net_package(configuration)
    missing_expected_target_file_locations = []

    for expected_location in expected_target_files:
        target_file_location = os.path.normpath(Path(expected_location).parent.parent)
        result = False
        for target_file in configuration.target_files:
            result = result or find_alongside_file(target_file_location, target_file)
        if not result:
            missing_expected_target_file_locations.append(target_file_location)
    return missing_expected_target_file_locations

# returns all missing target_files for a JAVA repo
def check_java_target_files(configuration):
    expected_target_files, omitted_target_files = get_java_package_roots(configuration)
    missing_expected_target_file_locations = []

    for expected_location in expected_target_files:
        result = False
        for target_file in configuration.target_files:
            result = result or find_alongside_file(expected_location, target_file)
        if not result:
            missing_expected_target_file_locations.append(os.path.dirname(expected_location))
    return missing_expected_target_file_locations
