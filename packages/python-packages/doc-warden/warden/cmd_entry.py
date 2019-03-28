# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.
from __future__ import print_function

from .enforce_readme_presence import find_missing_readmes
from .enforce_readme_content import verify_readme_content
from .WardenConfiguration import WardenConfiguration
import os

# CONFIGURATION. ENTRY POINT. EXECUTION.
def console_entry_point():
    cfg = WardenConfiguration()
        
    if cfg.verbose_output:
        cfg.dump()

    command_selector = {
        'scan': all_operations,
        'content': verify_content,
        'presence': verify_presence
    }
    
    if cfg.command in command_selector:
        command_selector.get(cfg.command)(cfg)
    else:
        print('Unrecognized command invocation {}.'.format(cfg.command))
        exit(1)

# verify the content of the readmes only
def verify_content(config):
    content_results, ignored_content_results = verify_readme_content(config)
    output_content_results(content_results, config)

    if len(content_results) > 0:
        conclusion_message(config)
        exit(1)

# verify the presence of the readmes only
def verify_presence(config):
    presence_results, ignored_presence_results = find_missing_readmes(config)
    output_presence_results(presence_results, config)

    if len(presence_results) > 0:
        conclusion_message(config)
        exit(1)

# print content results
def output_content_results(readmes_with_issues, config):
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

# print presence 
def output_presence_results(missing_readme_paths, config):
    if len(missing_readme_paths):
        print('{0} missing readme{1} detected at:'.format(len(missing_readme_paths), 's' if len(missing_readme_paths) > 1 else ''))
        for path in missing_readme_paths:
            print(config.get_output_path(path))
        print()

# execute both presence and content verification
def all_operations(config):
    content_results, ignored_content_results = verify_readme_content(config)
    presence_results, ignored_presence_results = find_missing_readmes(config)

    output_presence_results(presence_results, config)
    output_content_results(content_results, config)

    if len(content_results) > 0 or len(presence_results) > 0:
        conclusion_message(config)
        exit(1)

# return the plural form of the string given a count > 1
def pluralize(string, plural_string, count):
    return plural_string if count > 1 else string

# final output. Could get longer or pull from a template in the future.
def conclusion_message(config):
    print('For a rundown on what you need to do to resolve this breaking issue ASAP, check out aka.ms/azure-sdk-analyze-failed')
