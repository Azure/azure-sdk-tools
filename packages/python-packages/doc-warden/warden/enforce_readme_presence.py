# ------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project 
# root for license information.
# ------------------------------------------------------------------------------
from __future__ import print_function

import argparse
import yaml
import pathlib
import os
import glob
import xml.etree.ElementTree as ET
import fnmatch
import zipfile
import re
from .WardenConfiguration import WardenConfiguration

# python 3 transitioned StringIO to be part of `io` module. 
# python 2 needs the old version however
try:
    from StringIO import StringIO
except ImportError:
    from io import StringIO

DEFAULT_LOCATION = '.docsettings.yml'

def return_true(param):
    return True

# default option for handling an uncrecognized language
def unrecognized_option(configuration):
    print('Argument {} provided is not a supported option'.format(configuration.scan_language))
    exit(1)

# CONFIGURATION. ENTRY POINT. EXECUTION.
def console_entry_point():
    cfg = WardenConfiguration()
    print(cfg.dump())

    command_selector = {
        'scan': scan_repo,
    }
    
    if cfg.command in command_selector:
        command_selector.get(cfg.command)(cfg)
    else:
        print('Unrecognized command invocation {}.'.format(cfg.command))
        exit(1)

def scan_repo(config):
    missing_readme_paths = check_package_readmes(config)
    results(missing_readme_paths, config)


# print results
def results(missing_readme_paths, config):
    if len(missing_readme_paths):
        print('{} missing readmes detected at:'.format(len(missing_readme_paths)))
        for path in missing_readme_paths:
            print(path.replace(os.path.normpath(config.target_directory), ''))
        exit(1)

# parent caller for language types
def check_package_readmes(configuration):
    language_selector = {
        'python': check_python_readmes,
        'js': check_js_readmes,
        'java': check_java_readmes,
        'net': check_net_readmes
    }

    return language_selector.get(configuration.scan_language.lower(), unrecognized_option)(configuration)

# return all missing readmes for a PYTHON repostiroy
def check_python_readmes(configuration):
    expected_readmes, omitted_readmes = get_file_sets(configuration, '*/setup.py')
    missing_expected_readme_locations = []

    for expected_location in expected_readmes:
        result = find_alongside_file(expected_location, 'readme.md') or find_alongside_file(expected_location, 'readme.rst')
        if not result:
            missing_expected_readme_locations.append(os.path.dirname(expected_location))

    return missing_expected_readme_locations

# return all missing readmes for a JAVASCRIPT repository
def check_js_readmes(configuration):
    expected_readmes, omitted_readmes = get_file_sets(configuration, '*/package.json')
    missing_expected_readme_locations = []

    for expected_location in expected_readmes:
        result = find_alongside_file(expected_location, 'readme.md')
        if not result:
            missing_expected_readme_locations.append(os.path.dirname(expected_location))

    return missing_expected_readme_locations

# return all missing readmes for a .NET repostory
def check_net_readmes(configuration):
    expected_readmes, omitted_readmes = get_file_sets(configuration, '*.csproj', is_net_csproj_package)
    missing_expected_readme_locations = []

    for expected_location in expected_readmes:
        result = find_alongside_file(expected_location, 'readme.md')
        if not result:
            missing_expected_readmes.append(os.path.dirname(expected_location))
    return missing_expected_readme_locations

# convention. omit test projects
def is_net_csproj_package(file_path):
    return "tests.csproj" not in file_path.lower()

# returns all missing readmes for a JAVA repo
def check_java_readmes(configuration):
    expected_readmes, omitted_readmes = get_file_sets(configuration, "*/pom.xml", is_java_pom_package_pom)
    missing_expected_readmes = []

    for expected_location in expected_readmes:
        result = find_alongside_file(expected_location, 'readme.md')
        if not result:
            missing_expected_readmes.append(os.path.dirname(expected_location))

    return missing_expected_readmes

# given a pom.xml, crack it open and ensure that it is actually a package pom (versus a parent pom)
def is_java_pom_package_pom(file_path):
    root = parse_pom(file_path)
    jar_tag = root.find('packaging')

    if jar_tag is not None:
        return jar_tag.text == 'jar'
    return False

# check the root of the target_directory for a master README 
def check_repo_root(configuration):
    if configuration.root_check_enabled:
        # check root for readme.md
        present_files = [f for f in os.listdir(configuration.target_directory) if os.path.isfile(os.path.join(configuration.target_directory, f))]
        return  any(x in [f.lower() for f in present_files] for x in ['readme.md', 'readme.rst'])
    return true

# given a file location or folder, check within or alongside for a target file
# case insensitive
def find_alongside_file(file_location, target_file_name):
    if not os.path.exists(file_location) or not target_file_name:
        return False
    containing_folder = ''
    if os.path.isdir(file_location):
        # we're already looking at a file location. just check for presence of target_file_name in listdir
        containing_folder = file_location
    else:
        # os.path.listdir(os.path.dirname(file_location))
        containing_folder = os.path.dirname(file_location)

    for x in os.listdir(containing_folder):
        if x.lower() == target_file_name.lower():
            return os.path.normpath(os.path.join(containing_folder, x))
    return False

# returns the two sets:
    # the set of files where we expect a readme to be present
    # and the set of files that we expect a readme to be present that have been explicitly omitted
def get_file_sets(configuration, target_pattern, lambda_check = None):
    expected_locations = walk_directory_for_pattern(configuration.target_directory, [target_pattern], lambda_check)
    
    omitted_files = get_omitted_files(configuration)

    return list(set(expected_locations) - set(omitted_files)), set(omitted_files).intersection(expected_locations) 

# gets the set of files in the target directory that have explicitly been omitted in the config settings
def get_omitted_files(configuration):
    target_directory = configuration.target_directory
    omitted_paths = []
    dirs = configuration.omitted_paths or []

    # single special case here. if wildcard match at the beginning, do not join, use the pattern as is
    adjusted_dirs = [pattern if pattern.startswith("*") else os.path.join(target_directory, pattern) for pattern in dirs]
    omitted_paths.extend(walk_directory_for_pattern(target_directory, adjusted_dirs, None))

    return omitted_paths

# Returns a list of files under a target directory. The files included will match any of the
# target_patterns AND the lambda_check function. 
def walk_directory_for_pattern(target_directory, target_patterns, lambda_check = None):
    expected_locations = []
    target_directory = os.path.normpath(target_directory)
    normalized_target_patterns = [os.path.normpath(pattern) for pattern in target_patterns]
    check_function = lambda_check or return_true

    # walk the folders, filter to the patterns established
    for folder, subfolders, files in os.walk(target_directory): 
        for file in files:
            file_path = os.path.join(folder, file)
            if check_match(file_path, normalized_target_patterns) and check_function(file_path):
                expected_locations.append(file_path)

    return expected_locations

# we want to walk the files as few times as possible. as such, for omitted_files, we provide a SET 
# of patterns that we want to omit. This function simply checks 
def check_match(file_path, normalized_target_patterns):
    return any([fnmatch.fnmatch(file_path, normalized_target_pattern) 
                for normalized_target_pattern in normalized_target_patterns])

# namespaces in xml really mess with xmlTree: https://bugs.python.org/issue18304
# this function provides a workaround for both parsing an xml file as well as REMOVING said namespaces
def parse_pom(file_path):
    with open(file_path) as f:
        xml = f.read()

    it = ET.iterparse(StringIO(xml))
    for _, el in it:
        if '}' in el.tag:
            el.tag = el.tag.split('}', 1)[1]
    return it.root
