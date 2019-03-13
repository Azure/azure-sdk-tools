# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from __future__ import print_function
import argparse
import yaml
import os

class WardenConfiguration():
    def __init__(self):
        parser = argparse.ArgumentParser(description ='''\
        Scan an azure-sdk repo and ensure that readmes are present and have appropriate content. 
        This check is done by convention, which is why there is the --scan-language option exists. 
        For normal/CI usage, a .docsettings file should be present in the repository root, to allow
        for visible configuration of all the options.
        ''')

        parser.add_argument(
            '-d',
            '--scan-directory',
            dest = 'scan_directory',
            help = 'The repo directory that this tool should be scanning.',
            required = True)
        parser.add_argument(
            '-c',
            '--config-location',
            dest = 'config_location',
            required = False,
            help = '''
                  If provided, will replace the repo native .docsettings file 
                  with a .docsettings file found at the location provided by this input
                  ''')
        parser.add_argument(
            '-l',
            '--scan-language',
            dest = 'scan_language',
            required = False,
            help = 'The language contained within the target directory. Overrides .docsettings contents.')
        parser.add_argument(
            '-r',
            '--root-check-enabled',
            dest = 'root_check_enabled',
            required = False,
            help = 'Enable or disable checking for a readme at the root of the repository. Defaults true. Overrides .docsettings contents.')
        parser.add_argument(
            '-o',
            '--verbose-output',
            dest = 'verbose_output',
            required = False,
            help = 'Enable or disable verbose output. Defaults false. Overrides .docsettings contents.')
        parser.add_argument(
            'command',
            help = ('The warden command to run.'))

        args = parser.parse_args()

        self.command = args.command
        self.target_directory = args.scan_directory
        self.yml_location = args.config_location or os.path.join(self.target_directory, '.docsettings.yml')

        with open(self.yml_location, 'r') as f:
            try:
                doc = yaml.safe_load(f)
            except err:
                print('Unable to parse .docsettings. Check the location of the file.')

        try:
            self.omitted_paths = doc['omitted_paths'] or []
        except:
            self.omitted_paths = []

        try:
            self.required_readme_sections = doc['required_readme_sections'] or []
        except:
            self.required_readme_sections = []

        try:
            self.known_content_issues = doc['known_content_issues'] or []
        except:
            self.known_content_issues = []

        try:
            self.known_presence_issues = doc['known_presence_issues'] or []
        except:
            self.known_presence_issues = []

        try:
            self.scan_language = args.scan_language or doc['language']
        except:
            print('.docsettings has no selected language, neither has the --scan-language parameter been populated. Exiting.')
            exit(1)

        try:
            settings_file_root_check = doc['root_check_enabled']
        except:
            settings_file_root_check = False
        self.root_check_enabled = args.root_check_enabled or settings_file_root_check or True

        try:
            settings_file_verbose_output = doc['verbose_output']
        except:
            settings_file_verbose_output = False
        self.verbose_output = args.verbose_output or settings_file_verbose_output or False

    # strips the directory up till the repo root. Allows us to easily think about 
    # relative paths instead of absolute on disk
    def get_output_path(self, input_path):
        return input_path.replace(os.path.normpath(self.target_directory), '')

    def get_known_presence_issues(self):
        return [os.path.normpath(os.path.join(self.target_directory, exception_tuple[0])) for exception_tuple in self.known_presence_issues]

    def get_known_content_issues(self):
        return [os.path.normpath(os.path.join(self.target_directory, exception_tuple[0])) for exception_tuple in self.known_content_issues]

    def get_readme_sections_dictionary(self):
        return { key: i for i, key in enumerate(self.required_readme_sections) }

    def dump(self):
        current_config = {
            'command': self.command,
            'target_directory': self.target_directory,
            'yml_location': self.yml_location,
            'omitted_paths': self.omitted_paths,
            'scan_language': self.scan_language,
            'root_check_enabled': self.root_check_enabled,
            'verbose_output': self.verbose_output,
            'required_readme_sections': self.required_readme_sections,
            'known_content_issues': self.known_content_issues,
            'known_presence_issues': self.known_presence_issues
        }

        print("Warden configuration this run:")
        for key in current_config:
            print('{0}: {1}'.format(key, current_config[key]))

        return current_config
