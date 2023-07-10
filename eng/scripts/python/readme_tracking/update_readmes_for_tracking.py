from __future__ import print_function
import os
import fnmatch
import re
import sys
import urllib.parse
import argparse


HOSTNAME = 'https://azure-sdk-impressions.azurewebsites.net'

README_PATTERNS = ['*/readme.md', '*/readme.rst']
MARKDOWN_REGEX = r'!\[Impressions\]\([^\)]+\)'
RST_REGEX = r'\.\. image::  ' + HOSTNAME + r'/api/impressions/[^\s]+'

TRACKING_PIXEL_MD_FORMAT_STRING = '![Impressions](' + HOSTNAME + '/api/impressions/{0}{1})'
TRACKING_PIXEL_RST_FORMAT_STRING = '.. image::  ' + HOSTNAME + '/api/impressions/{0}{1}'

# walks a target directory with support for multiple glob patterns
def walk_directory_for_pattern(target_directory, target_patterns):
    expected_locations = []
    target_directory = os.path.normpath(target_directory)
    normalized_target_patterns = [os.path.normpath(pattern) for pattern in target_patterns]

    # walk the folders, filter to the patterns established
    for folder, subfolders, files in os.walk(target_directory):
        for file in files:
            file_path = os.path.join(folder, file)
            if check_match(file_path, normalized_target_patterns):
                expected_locations.append(file_path)

    return expected_locations

# a set of glob patterns against a single file path
def check_match(file_path, normalized_target_patterns):
    return any([fnmatch.fnmatch(file_path, normalized_target_pattern)
                for normalized_target_pattern in normalized_target_patterns])

# returns all readmes that match either of the readme patterns.
def get_all_readme_files(folder_location):
    return walk_directory_for_pattern(folder_location, README_PATTERNS)

# runs across provided set of readmes, inserts or updates pixels in all
def update_readmes_with_tracking(readme_files, target_directory, repo_id):
    for file_path in readme_files:
        with open(file_path, 'r') as f:
            data = f.read()

        md_regex = re.compile(MARKDOWN_REGEX, re.IGNORECASE | re.MULTILINE)
        rs_regex = re.compile(RST_REGEX, re.IGNORECASE | re.MULTILINE)
        try:
            extension = os.path.splitext(file_path)[1]
        except e:
            print('No file extension present.')
            print(e)
            exit(1)

        if (extension == '.rst'):
            updated_content = replace_tracking_pixel_rst(file_path, data, rs_regex, target_directory, repo_id)
        if (extension == '.md'):
            updated_content = replace_tracking_pixel_md(file_path, data, md_regex, target_directory, repo_id)

        if updated_content != data:
            with open(file_path, 'w') as f:
                f.write(updated_content)

# insert/update tracking pixel, rst specific
def replace_tracking_pixel_rst(file_path, file_content, compiled_regex, target_directory, repo_id):
    existing_matches = compiled_regex.search(file_content)

    if existing_matches:
        return compiled_regex.sub(get_tracking_pixel(TRACKING_PIXEL_RST_FORMAT_STRING, file_path, target_directory, repo_id), file_content)
    else:
        return file_content + '\n\n' + get_tracking_pixel(TRACKING_PIXEL_RST_FORMAT_STRING, file_path, target_directory, repo_id) + '\n'

# insert/update tracking pixel, markdown specific
def replace_tracking_pixel_md(file_path, file_content, compiled_regex, target_directory, repo_id):
    existing_matches = compiled_regex.search(file_content)

    if existing_matches:
        return compiled_regex.sub(get_tracking_pixel(TRACKING_PIXEL_MD_FORMAT_STRING, file_path, target_directory, repo_id), file_content)
    else:
        return file_content + '\n\n' + get_tracking_pixel(TRACKING_PIXEL_MD_FORMAT_STRING, file_path, target_directory, repo_id) + '\n'

# creates the pixel tag
def get_tracking_pixel(fmt_string, file_path, target_directory, repo_id):
    # remove leading folders
    relative_path = file_path.replace(os.path.normpath(target_directory), '')

    # rename to an image
    relative_path = os.path.splitext(relative_path)[0] + '.png'

    # ensure that windows doesn't mess up anything
    slash_path = relative_path.replace('\\', '/')

    # uri encode
    url_encoded_path = urllib.parse.quote_plus(slash_path)

    return fmt_string.format(repo_id, url_encoded_path)

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description ='Script that will take any repository + identifier as input, and add or update a tracking pixel image in all readme files contained within the input directory.')

    parser.add_argument(
        '-d',
        '--scan-directory',
        dest = 'scan_directory',
        help = 'The repo directory that this tool should be scanning.',
        required = True)
    parser.add_argument(
        '-i',
        '--id',
        dest = 'repo_id',
        help = 'The repository identifier. Will be prefixed onto the readme path.',
        required = True)
    args = parser.parse_args()

    args.scan_directory = os.path.abspath(args.scan_directory)
    target_readme_files = get_all_readme_files(args.scan_directory)
    update_readmes_with_tracking(target_readme_files, args.scan_directory, args.repo_id)
