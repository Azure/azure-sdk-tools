# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import os
import fnmatch

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

# gets the set of files in the target directory that have explicitly been omitted in the config settings
def get_omitted_files(configuration):
    target_directory = configuration.target_directory
    omitted_paths = []
    dirs = configuration.omitted_paths or []

    # single special case here. if wildcard match at the beginning, do not join, use the pattern as is
    adjusted_dirs = [pattern if pattern.startswith("*") else os.path.join(target_directory, pattern) for pattern in dirs]
    omitted_paths.extend(walk_directory_for_pattern(target_directory, adjusted_dirs, None))

    return omitted_paths

# we want to walk the files as few times as possible. as such, for omitted_files, we provide a SET 
# of patterns that we want to omit. This function simply checks 
def check_match(file_path, normalized_target_patterns):
    return any([fnmatch.fnmatch(file_path, normalized_target_pattern) 
                for normalized_target_pattern in normalized_target_patterns])

def return_true(param):
    return True
