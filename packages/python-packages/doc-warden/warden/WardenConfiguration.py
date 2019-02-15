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
            dest = 'output_report',
            required = False,
            help = 'Enable or disable html generation.')
        parser.add_argument(
            'command',
            help = ('The warden command to run.'))

        args = parser.parse_args()

        self.command = args.command
        self.target_directory = args.scan_directory
        self.yml_location = args.config_location or os.path.join(self.target_directory, '.docsettings.yml')

        with open(self.yml_location, 'r') as f:
            try:
                doc = yaml.load(f)
            except err:
                print('Unable to parse .docsettings. Check the location of the file.')

        try:
            self.omitted_paths = doc['omitted_paths']
        except:
            self.omitted_paths = []

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

        self.verbose_output = args.output_report or False

    def dump(self):
        return {
            'command': self.command,
            'target_directory': self.target_directory,
            'yml_location': self.yml_location,
            'omitted_paths': self.omitted_paths,
            'scan_language': self.scan_language,
            'root_check_enabled': self.root_check_enabled,
            'verbose_output': self.verbose_output
        }
