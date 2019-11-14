# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from __future__ import print_function

import pathlib
import os
import glob
import fnmatch
import zipfile
from .warden_common import get_java_package_roots, get_net_package_roots, get_python_package_roots, get_js_package_roots, find_alongside_file

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
def find_missing_readmes(configuration):
    language_selector = {
        'python': check_python_readmes,
        'js': check_js_readmes,
        'java': check_java_readmes,
        'net': check_net_readmes
    }
    missing_readme_paths = []
    ignored_missing_readme_paths = []
    known_issue_paths = configuration.get_known_presence_issues()
    readme_results = language_selector.get(configuration.scan_language.lower(), unrecognized_option)(configuration)

    for readme_path in readme_results:
        if readme_path in known_issue_paths:
            ignored_missing_readme_paths.append(readme_path)
        else:
            missing_readme_paths.append(readme_path)

    return missing_readme_paths, ignored_missing_readme_paths

# check the root of the target_directory for a master README 
def check_repo_root(configuration):
    if configuration.root_check_enabled:
        # check root for readme.md
        present_files = [f for f in os.listdir(configuration.target_directory) if os.path.isfile(os.path.join(configuration.target_directory, f))]
        return  any(x in [f.lower() for f in present_files] for x in ['readme.md', 'readme.rst'])
    return true

# return all missing readmes for a PYTHON repostiroy
def check_python_readmes(configuration):
    expected_readmes, omitted_readmes = get_python_package_roots(configuration)
    missing_expected_readme_locations = []

    for expected_location in expected_readmes:
        result = find_alongside_file(expected_location, 'readme.md') or find_alongside_file(expected_location, 'readme.rst')
        if not result:
            missing_expected_readme_locations.append(os.path.dirname(expected_location))

    return missing_expected_readme_locations

# return all missing readmes for a JAVASCRIPT repository
def check_js_readmes(configuration):
    expected_readmes, omitted_readmes = get_js_package_roots(configuration)
    missing_expected_readme_locations = []

    for expected_location in expected_readmes:
        result = find_alongside_file(expected_location, 'readme.md')
        if not result:
            missing_expected_readme_locations.append(os.path.dirname(expected_location))

    return missing_expected_readme_locations

# return all missing readmes for a .NET repostory
def check_net_readmes(configuration):
    expected_readmes, omitted_readmes = get_net_package_roots(configuration)
    missing_expected_readme_locations = []

    for expected_location in expected_readmes:
        result = find_alongside_file(expected_location, 'readme.md')
        if not result:
            missing_expected_readme_locations.append(os.path.dirname(expected_location))
    return missing_expected_readme_locations

# returns all missing readmes for a JAVA repo
def check_java_readmes(configuration):
    expected_readmes, omitted_readmes = get_java_package_roots(configuration)
    missing_expected_readmes = []

    for expected_location in expected_readmes:
        result = find_alongside_file(expected_location, 'readme.md')
        if not result:
            missing_expected_readmes.append(os.path.dirname(expected_location))

    return missing_expected_readmes
