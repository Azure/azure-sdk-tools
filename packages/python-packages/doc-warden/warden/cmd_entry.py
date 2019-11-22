# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.
from __future__ import print_function

from .enforce_target_file_presence import find_missing_target_files
from .enforce_readme_content import verify_readme_content
from .enforce_changelog_content import verify_changelog_content
from .index_packages import index_packages, render
from .WardenConfiguration import WardenConfiguration
from .PackageInfo import PackageInfo
import os
import logging

# CONFIGURATION. ENTRY POINT. EXECUTION.
def console_entry_point():
    cfg = WardenConfiguration()
        
    if cfg.verbose_output:
        cfg.dump()

    command_selector = {
        'scan': all_operations,
        'content': verify_content,
        'presence': verify_presence,
        'index': index
    }
    
    if cfg.command in command_selector:
        command_selector.get(cfg.command)(cfg)
    else:
        print('Unrecognized command invocation {}.'.format(cfg.command))
        exit(1)

# index the packages present in the repository
def index(config):
    packages = index_packages(config)
    render(config, packages)

    if config.verbose_output:
        print('Warden located the following packages: ')
        for pkg in packages:
            print(pkg.package_id)

# verify the content of readmes or changelogs
def verify_content(config):
    if 'readme' in config.target_files[0]:
        content_results, ignored_content_results = verify_readme_content(config)
        output_readme_content_results(content_results, config)
        exit_on_readme_content_issues(content_results, config)

    if 'changelog' in config.target_files[0]:
        missing_changelog, empty_release_notes, pkg_list = verify_changelog_content(config)
        output_changelog_content_results(missing_changelog, empty_release_notes)
        exit_on_changelog_content_issues(missing_changelog, empty_release_notes, config)

# verify the presence of the target_files (Readme or Changelog)
def verify_presence(config):
    presence_results, ignored_presence_results = find_missing_target_files(config)
    output_presence_results(presence_results, config)

    if len(presence_results) > 0:
        conclusion_message()
        exit(1)

# Verify Case of files Present
def verify_file_case(pkg_list, config):
    readmes_with_wrong_case = []
    changelogs_with_wrong_case = []
    for pkg in pkg_list:
        if pkg.relative_readme_location.lower().endswith('readme.md'):
            if not pkg.relative_readme_location.endswith('README.md'):
                readmes_with_wrong_case.append(os.path.normpath(os.path.join(config.target_directory, pkg.relative_readme_location)))
        if pkg.relative_changelog_location.lower().endswith('changelog.md'):
            if not pkg.relative_changelog_location.endswith('CHANGELOG.md'):
                changelogs_with_wrong_case.append(os.path.normpath(os.path.join(config.target_directory, pkg.relative_changelog_location)))
    return readmes_with_wrong_case, changelogs_with_wrong_case

# Exit if there are readme content issues
def exit_on_readme_content_issues(content_results, config):
    if len(content_results) > 0:
        conclusion_message()
        exit(1)

# Exit if there are changelog content issues
def exit_on_changelog_content_issues(missing_changelog, empty_release_notes, config):
    if len(missing_changelog) > 0:
        conclusion_message()
        exit(1)

    if config.pipeline_stage == 'release' and len(empty_release_notes) > 0:
        conclusion_message()
        exit(1)

# print content results for readme
def output_readme_content_results(readmes_with_issues, config):
    length = len(readmes_with_issues)
    if length:
        print('{0} {1} at least one missing required section.'.format(length, pluralize('readme has', 'readmes have', length)))
        for readme_tuple in readmes_with_issues:
            header = '{0} is missing {1} with {2}:'.format(
                        config.get_output_path(readme_tuple[0]), 
                        pluralize('a header', 'headers', len(readme_tuple[1])),
                        pluralize('the pattern', 'patterns', len(readme_tuple[1]))
                        )
            print(header)

            for missing_pattern in readme_tuple[1]:
                print(' * {0}'.format(missing_pattern))

            print()

# print content results for changelog
def output_changelog_content_results(missing_changelog, empty_release_notes):
    if len(missing_changelog):
        print('{0} {1} missing entry{2} for the latest package version'.format(len(missing_changelog), pluralize('changelog has', 'changelogs have', len(missing_changelog)), pluralize('', 's', len(missing_changelog))))
        print()
        for changelog_tuple in missing_changelog:
            print('MISSING CHANGELOG ENTRY: Latest Version {0} is missing in {1}. Add changelog for latest version'.format(changelog_tuple[1]['curr_pkg_version'], changelog_tuple[0]))
        print()

    if len(empty_release_notes):
        print('{0} {1} empty release note for the latest package version'.format(len(empty_release_notes), pluralize('changelog has', 'changelogs have', len(empty_release_notes))))
        print()
        for changelog_tuple in empty_release_notes:
            print('EMPTY CHANGELOG ENTRY: Latest Version {0} has no release notes in {1}. Consider adding release notes'.format(changelog_tuple[1]['curr_pkg_version'], changelog_tuple[0]))
        print()

# print presence results
def output_presence_results(missing_target_file_paths, config):
    if len(missing_target_file_paths):
        print('{0} missing {1}{2} detected at:'.format(len(missing_target_file_paths), config.target_files[0], 's' if len(missing_target_file_paths) > 1 else ''))
        for path in missing_target_file_paths:
            print(config.get_output_path(path))
        print()

# print case issues
def output_case_results(readmes_with_wrong_case, changelogs_with_wrong_case):
    if len(readmes_with_wrong_case):
        print('{0} Readme{1} are wrongly named:'.format(len(readmes_with_wrong_case), 's' if len(readmes_with_wrong_case) > 1 else ''))
        for path in readmes_with_wrong_case:
            print(path)
        print()

    if len(changelogs_with_wrong_case):
        print('{0} Changelog{1} are wrongly named:'.format(len(changelogs_with_wrong_case), 's' if len(changelogs_with_wrong_case) > 1 else ''))
        for path in changelogs_with_wrong_case:
            print(path)
        print()


# execute both presence and content verification
def all_operations(config):

    if config.verbose_output:
        print('Starting Readme Presence Examination')

    readme_presence_results, ignored_readme_presence_results = find_missing_target_files(config)
    if config.verbose_output:
        print('Done with Readme Presence Examination')
        print('Starting Readme Content Examination')

    readme_content_results, ignored_readme_content_results = verify_readme_content(config)
    if config.verbose_output:
        print('Done with Readme Content Examination')

    output_presence_results(readme_presence_results, config)
    output_readme_content_results(readme_content_results, config)

    config.target_files = ['changelog.md'] # Switch Config to scan for changelog
    if config.verbose_output:
        print('Starting Changelog Presence Examination')

    changelog_presence_results, ignored_changelog_presence_results = find_missing_target_files(config)
    if config.verbose_output:
        print('Done with Changelog Presence Examination')
        print('Starting Changelog Content Examination')

    missing_changelog, empty_release_notes, pkg_list = verify_changelog_content(config)
    if config.verbose_output:
        print('Done with Changelog Content Examination')

    readmes_with_wrong_case, changelogs_with_wrong_case = verify_file_case(pkg_list, config)

    output_presence_results(changelog_presence_results, config)
    output_changelog_content_results(missing_changelog, empty_release_notes)
    output_case_results(readmes_with_wrong_case, changelogs_with_wrong_case)

    exit_on_readme_content_issues(readme_content_results, config)
    exit_on_changelog_content_issues(missing_changelog, empty_release_notes, config)

    if len(readme_presence_results) > 0 or len(changelog_presence_results) > 0 or readmes_with_wrong_case > 0 or changelogs_with_wrong_case > 0:
        conclusion_message()
        exit(1)



# return the plural form of the string given a count > 1
def pluralize(string, plural_string, count):
    return plural_string if count > 1 else string

# final output. Could get longer or pull from a template in the future.
def conclusion_message():
    print('For a rundown on what you need to do to resolve this breaking issue ASAP, check out aka.ms/azure-sdk-analyze-failed')
